using BaggageDelivery.Core.Interfaces;
using Serilog;

namespace BaggageDelivery.Api.Dev;

public static class DevStartup
{
    public const string DefaultPaxBaseUrl = "http://baggagedelivery.local.deliverdifferent.com:5173";

    private const int DefaultJobId = 67;

    internal sealed record DevLinks(int JobId, string Token, string ConfirmUrl);

    internal sealed record DevLinksResult(DevLinks? Links, string? Error, Exception? Exception = null);

    internal static string ResolvePaxBaseUrl(string? appUrl) =>
        string.IsNullOrWhiteSpace(appUrl) ? DefaultPaxBaseUrl : appUrl.TrimEnd('/');

    internal static DevLinksResult BuildLinks(IEncryptionService? encryptor, int jobId, string? appUrl)
    {
        if (encryptor is null)
        {
            return new DevLinksResult(null, "IEncryptionService not registered");
        }

        string token;
        try
        {
            token = encryptor.EncryptId(jobId);
        }
        catch (Exception ex)
        {
            return new DevLinksResult(null,
                "failed to mint magic-link token (likely missing BaggageDeliveryEncryptionKey/IV env vars)",
                ex);
        }

        var paxBase = ResolvePaxBaseUrl(appUrl);

        return new DevLinksResult(
            new DevLinks(jobId, token, $"{paxBase}/c/{token}"),
            null);
    }

    private static int ResolveJobId(IConfiguration configuration) =>
        configuration.GetSection("DevTesting").GetValue("JobId", DefaultJobId);

    extension(WebApplication app)
    {
        public void LogTestMagicLinks()
        {
            if (!app.Environment.IsDevelopment())
            {
                return;
            }

            using var scope = app.Services.CreateScope();
            var result = BuildLinks(
                scope.ServiceProvider.GetService<IEncryptionService>(),
                ResolveJobId(app.Configuration),
                Environment.GetEnvironmentVariable("AppUrl"));

            if (result.Links is null)
            {
                Log.Warning(result.Exception, "DevStartup: {Reason} — skipping magic-link URLs", result.Error);
                return;
            }

            Log.Information("DevStartup: magic links for JobId={JobId}", result.Links.JobId);
            Log.Information("  Pax confirmation: {ConfirmUrl}", result.Links.ConfirmUrl);
        }

        public void MapDevLinks()
        {
            if (!app.Environment.IsDevelopment())
            {
                return;
            }

            app.MapGet("/api/v1/dev/links", (IConfiguration configuration, IServiceProvider services) =>
                {
                    var result = BuildLinks(
                        services.GetService<IEncryptionService>(),
                        ResolveJobId(configuration),
                        Environment.GetEnvironmentVariable("AppUrl"));

                    return result.Links is null
                        ? Results.Problem(result.Error, statusCode: StatusCodes.Status503ServiceUnavailable)
                        : Results.Ok(result.Links);
                })
                .AllowAnonymous();
        }
    }
}
