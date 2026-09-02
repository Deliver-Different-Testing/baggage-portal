using BaggageDelivery.Core.Http;
using BaggageDelivery.Core.Interfaces;
using BaggageDelivery.Core.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;

namespace BaggageDelivery.Core.Services;

internal sealed class JobTrackingLinkService(
    BaggageDeliveryContext db,
    IOptions<TrackingPageUrlsOptions> options) : IJobTrackingLinkService
{
    public async Task<string?> GetTrackingUrlAsync(int jobId, CancellationToken ct)
    {
        if (options.Value.BaseUrl is not { } baseUrl)
        {
            return null;
        }

        string? token;
        try
        {
            token = await db.Database
                .SqlQueryRaw<string>(
                    "SELECT dbo.EncryptJobIdReversible(@JobID) AS Value",
                    new SqlParameter("@JobID", jobId))
                .FirstOrDefaultAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Warning(ex, "Could not build tracking URL for JobId={JobId}", jobId);
            return null;
        }

        return string.IsNullOrWhiteSpace(token)
            ? null
            : $"{baseUrl.ToString().TrimEnd('/')}/#/{token}";
    }
}
