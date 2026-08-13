using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading.RateLimiting;
using BaggageDelivery.Api.Auth;
using BaggageDelivery.Api.Dev;
using BaggageDelivery.Core;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.FileProviders;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Environment.WebRootPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot");

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console()
    .CreateLogger();
builder.Host.UseSerilog();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment.IsDevelopment());
builder.Services.AddAppAuthentication(builder.Configuration, builder.Environment.IsDevelopment());

// Must be AddControllersWithViews, not AddControllers: the ViewFeatures services it
// brings in are what supply ValidateAntiforgeryTokenAuthorizationFilter for
// [ValidateAntiForgeryToken] on PaxBookingController.
builder.Services.AddControllersWithViews(options => { options.MaxModelBindingCollectionSize = 100; });
builder.WebHost.ConfigureKestrel(options => { options.Limits.MaxRequestBodySize = 4 * 1024 * 1024; });

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("fixed", limiterOptions =>
    {
        limiterOptions.PermitLimit = 100;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 10;
    });
    options.AddFixedWindowLimiter("admin-mint", limiterOptions =>
    {
        limiterOptions.PermitLimit = 60;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 10;
    });
});

if (builder.Environment.IsDevelopment())
{
    var keyDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DeliverDifferent", "DataProtection-Keys");

    if (!Directory.Exists(keyDirectory))
    {
        var dirInfo = Directory.CreateDirectory(keyDirectory);
        if (OperatingSystem.IsWindows())
        {
            var currentUser = WindowsIdentity.GetCurrent();
            var accessRule = new FileSystemAccessRule(currentUser.Name,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None, AccessControlType.Allow);
            var security = dirInfo.GetAccessControl();
            security.AddAccessRule(accessRule);
            dirInfo.SetAccessControl(security);
        }
    }

    var dp = builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(keyDirectory))
        .SetApplicationName("DeliverDifferent");

    if (OperatingSystem.IsWindows())
    {
        dp.ProtectKeysWithDpapi();
    }
}
else
{
    builder.Services.AddDataProtection()
        .PersistKeysToAWSSystemsManager("/Hub/DataProtection")
        .SetApplicationName("DeliverDifferent");
}

builder.Services.AddHealthChecks();

builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "BaggageDelivery API";
        document.Info.Version = "v1";
        document.Info.Description = "Passenger-facing baggage delivery confirmation and tracking";
        return Task.CompletedTask;
    });
});

var appUrl = Environment.GetEnvironmentVariable("AppUrl")
             ?? "http://baggagedelivery.local.deliverdifferent.com:5173";
var allowedOrigins = new List<string> { appUrl.TrimEnd('/') };
if (builder.Environment.IsDevelopment())
{
    allowedOrigins.Add("http://localhost:5173");
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowPaxPortal", policy =>
    {
        policy.WithOrigins([.. allowedOrigins])
            .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
            .WithHeaders("Content-Type", "X-Requested-With", "Accept", "Authorization", "X-XSRF-TOKEN")
            .AllowCredentials();
    });
});

var app = builder.Build();

app.UseForwardedHeaders();

app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.XFrameOptions = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] =
        "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), usb=()";
    context.Response.Headers.ContentSecurityPolicy =
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:; font-src 'self' data:; connect-src 'self'; manifest-src 'self'; frame-ancestors 'none';";

    if (context.Request.IsHttps)
    {
        context.Response.Headers.StrictTransportSecurity = "max-age=31536000; includeSubDomains";
    }

    await next();
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (Directory.Exists(app.Environment.WebRootPath))
{
    var fileProvider = new PhysicalFileProvider(app.Environment.WebRootPath);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = fileProvider });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = fileProvider,
        OnPrepareResponse = ctx =>
        {
            ctx.Context.Response.Headers.CacheControl =
                ctx.File.Name is "index.html" or "sw.js" or "registerSW.js" or "manifest.webmanifest"
                    ? "no-cache, no-store"
                    : "public, max-age=31536000, immutable";
        }
    });
}
else
{
    Log.Warning("wwwroot not found at {WebRootPath} - static file serving disabled (expected in local dev)",
        app.Environment.WebRootPath);
}

app.UseRateLimiter();
app.UseCors("AllowPaxPortal");

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/healthz");

app.MapControllers();

if (Directory.Exists(app.Environment.WebRootPath))
{
    app.MapFallbackToFile("index.html").AllowAnonymous();
}

app.MapDevLinks();
app.LogTestMagicLinks();

app.Run();

// Exposes the top-level-statements entry point to WebApplicationFactory<Program>.
public partial class Program;