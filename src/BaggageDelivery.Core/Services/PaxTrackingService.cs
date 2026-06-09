using BaggageDelivery.Core.Http;
using BaggageDelivery.Core.Http.Models;
using BaggageDelivery.Core.MultiTenant;

namespace BaggageDelivery.Core.Services;

internal sealed class PaxTrackingService(IDespatchApiClient despatch, ITenantResolver tenants) : IPaxTrackingService
{
    public async Task<TrackingDto?> GetTimelineAsync(int jobId, int tenantId, CancellationToken ct)
    {
        var ctx = await tenants.ResolveAsync(tenantId, ct);
        return await despatch.GetJobTrackingAsync(
            tenantId, ctx.Connection, ctx.TimeZone, clientId: null, contactId: 0, jobId, ct);
    }
}
