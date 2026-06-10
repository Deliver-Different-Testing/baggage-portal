using BaggageDelivery.Core.Http.Models;
using BaggageDelivery.Core.Interfaces;
using Serilog;

namespace BaggageDelivery.Core.Services;

internal sealed class PaxTrackingService(
    ITrackingPageClient trackingPage,
    ITenantResolver tenants) : IPaxTrackingService
{
    public async Task<TrackingDto?> GetTimelineAsync(int tenantId, int jobId, CancellationToken ct)
    {
        // Tenant lookup is kept as the stale-link guard — if a tenant has
        // been retired we want the passenger to land on /expired rather
        // than render a tracking page for a job they no longer own. The
        // trackingpage HTTP call itself is anonymous.
        try
        {
            _ = await tenants.ResolveAsync(tenantId, ct);
        }
        catch (InvalidOperationException ex)
        {
            Log.Warning(ex, "GetTimeline: tenant {TenantId} is not configured", tenantId);
            return null;
        }

        return await trackingPage.GetJobAsync(jobId, ct);
    }
}
