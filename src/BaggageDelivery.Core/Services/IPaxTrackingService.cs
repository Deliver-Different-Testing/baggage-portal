using BaggageDelivery.Core.Http.Models;

namespace BaggageDelivery.Core.Services;

public interface IPaxTrackingService
{
    Task<TrackingDto?> GetTimelineAsync(int jobId, int tenantId, CancellationToken ct);
}
