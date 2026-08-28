using BaggageDelivery.Core.Http.Models;

namespace BaggageDelivery.Core.Interfaces;

public interface ITrackingPageClient
{
    Task<TrackingDto?> GetJobAsync(int jobId, CancellationToken ct);
}
