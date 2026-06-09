using BaggageDelivery.Core.Http.Models;
using BaggageDelivery.Core.Interfaces;
using BaggageDelivery.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace BaggageDelivery.Core.Services;

internal sealed class PaxTrackingService(
    BaggageDeliveryContext db,
    IDespatchApiClient despatch,
    ITenantResolver tenants) : IPaxTrackingService
{
    public async Task<TrackingDto?> GetTimelineAsync(int bookingId, CancellationToken ct)
    {
        var routing = await db.BagDelBookings
            .AsNoTracking()
            .Where(b => b.Id == bookingId)
            .Select(b => new { b.TenantId, b.JobId })
            .FirstOrDefaultAsync(ct);

        if (routing is null)
        {
            return null;
        }

        var ctx = await tenants.ResolveAsync(routing.TenantId, ct);
        return await despatch.GetJobTrackingAsync(
            routing.TenantId, ctx.Connection, ctx.TimeZone, clientId: null, contactId: 0, routing.JobId, ct);
    }
}
