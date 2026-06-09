using BaggageDelivery.Core.Http.Models;
using BaggageDelivery.Core.Interfaces;
using Serilog;

namespace BaggageDelivery.Core.Services;

internal sealed class PaxTrackingService(
    IDespatchApiClient despatch,
    ITenantResolver tenants) : IPaxTrackingService
{
    public async Task<TrackingDto?> GetTimelineAsync(int tenantId, int jobId, CancellationToken ct)
    {
        try
        {
            var ctx = await tenants.ResolveAsync(tenantId, ct);
            return await despatch.GetJobTrackingAsync(
                tenantId, ctx.Connection, ctx.TimeZone, clientId: null, contactId: 0, jobId, ct);
        }
        catch (InvalidOperationException ex)
        {
            Log.Warning(ex, "GetTimeline: tenant {TenantId} is not configured", tenantId);
            return null;
        }
    }
}
