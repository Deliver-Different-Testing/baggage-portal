using BaggageDelivery.Core.Interfaces;
using Serilog;

namespace BaggageDelivery.Api.Dev;

public static class DevStartup
{
    // Dev-only startup hook: mints the encrypted pax token for a known
    // tucJob and logs the magic-link URLs so a developer can copy-paste
    // them into a browser to exercise /c (confirmation) and /t (tracking)
    // without going through IM's BDO ingest flow.
    //
    // Configurable via DevTesting:JobId or DevTesting__JobId env var.
    // Default matches the agreed test job (67).
    extension(WebApplication app)
    {
        public void LogTestMagicLinks()
        {
            if (!app.Environment.IsDevelopment())
            {
                return;
            }

            var jobId = app.Configuration.GetSection("DevTesting").GetValue("JobId", 67);

            var paxBase = Environment.GetEnvironmentVariable("AppUrl")?.TrimEnd('/')
                ?? "http://baggagedelivery.local.deliverdifferent.com:5173";

            using var scope = app.Services.CreateScope();
            var encryptor = scope.ServiceProvider.GetService<IEncryptionService>();
            if (encryptor is null)
            {
                Log.Warning("DevStartup: IEncryptionService not registered — skipping magic-link URLs");
                return;
            }

            string token;
            try
            {
                token = encryptor.EncryptId(jobId);
            }
            catch (Exception ex)
            {
                Log.Warning(ex,
                    "DevStartup: failed to mint magic-link token (likely missing BaggageDeliveryEncryptionKey/IV env vars)");
                return;
            }

            Log.Information("DevStartup: magic links for JobId={JobId}", jobId);
            Log.Information("  Pax confirmation: {ConfirmUrl}", $"{paxBase}/c/{token}");
            Log.Information("  Pax tracking:     {TrackUrl}", $"{paxBase}/t/{token}");
        }
    }
}
