using BaggageDelivery.Core.Http;
using BaggageDelivery.Core.Http.Models;
using BaggageDelivery.Core.Models;
using BaggageDelivery.Core.MultiTenant;
using Microsoft.EntityFrameworkCore;

namespace BaggageDelivery.Core.Services;

internal sealed class PaxTrackingService(
    BaggageDeliveryContext db,
    IDespatchApiClient despatch,
    ITenantResolver tenants) : IPaxTrackingService
{
    public async Task<TrackingDto?> GetTimelineAsync(int bookingId, CancellationToken ct)
    {
        var booking = await db.BagDelBookings
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == bookingId, ct);

        if (booking is null)
        {
            return null;
        }

        var ctx = await tenants.ResolveAsync(booking.TenantId, ct);
        return await despatch.GetJobTrackingAsync(
            booking.TenantId, ctx.Connection, ctx.TimeZone, clientId: null, contactId: 0, booking.JobId, ct);
    }
}
