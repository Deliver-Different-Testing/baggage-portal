using BaggageDelivery.Core.Http.Models;

namespace BaggageDelivery.Core.Interfaces;

public interface IPaxTrackingService
{
    Task<TrackingDto?> GetTimelineAsync(int tenantId, int jobId, CancellationToken ct);
}
