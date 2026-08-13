using BaggageDelivery.Core.Interfaces;
using Serilog;

namespace BaggageDelivery.Api.Dev;

public static class DevStartup
{
    // Dev-only magic links: mints the encrypted pax token for a known tucJob so a
    // developer can exercise /c (confirmation) and /t (tracking) without going
    // through IM's BDO ingest flow. Surfaced two ways — logged at startup by
    // LogTestMagicLinks, and served by GET /api/v1/dev/links for the SPA's
    // landing page on /. Both go through BuildLinks so they can't drift.
    //
    // Configurable via DevTesting:JobId or DevTesting__JobId env var.
    // Default matches the agreed test job (67).
    public const string DefaultPaxBaseUrl = "http://baggagedelivery.local.deliverdifferent.com:5173";

    private const int DefaultJobId = 67;

    internal sealed record DevLinks(int JobId, string Token, string ConfirmUrl, string TrackUrl);

    internal sealed record DevLinksResult(DevLinks? Links, string? Error, Exception? Exception = null);

    // The pax flow is served by the Vite dev server, not by Kestrel, so the links
    // must point at AppUrl (the SPA origin) rather than at the API's own address.
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
            new DevLinks(jobId, token, $"{paxBase}/c/{token}", $"{paxBase}/t/{token}"),
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
            Log.Information("  Pax tracking:     {TrackUrl}", result.Links.TrackUrl);
        }

        // Only mapped in Development, so the route genuinely does not exist in any
        // other environment — the SPA's / route relies on that 404 to fall back to
        // /expired, which is the production behaviour.
        public void MapDevLinks()
        {
            if (!app.Environment.IsDevelopment())
            {
                return;
            }

            // Resolved through IServiceProvider rather than as a handler parameter so a
            // missing IEncryptionService yields the 503 below instead of a binding failure.
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
