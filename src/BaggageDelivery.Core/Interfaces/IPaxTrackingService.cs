using BaggageDelivery.Core.Http.Models;

namespace BaggageDelivery.Core.Interfaces;

public interface IPaxTrackingService
{
    Task<TrackingDto?> GetTimelineAsync(int bookingId, CancellationToken ct);
}
