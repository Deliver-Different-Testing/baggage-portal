using BaggageDelivery.Core.Http.Models;
using BaggageDelivery.Core.Interfaces;

namespace BaggageDelivery.Core.Services;

internal sealed class PaxTrackingService(ITrackingPageClient trackingPage) : IPaxTrackingService
{
    public Task<TrackingDto?> GetTimelineAsync(int jobId, CancellationToken ct) =>
        trackingPage.GetJobAsync(jobId, ct);
}
