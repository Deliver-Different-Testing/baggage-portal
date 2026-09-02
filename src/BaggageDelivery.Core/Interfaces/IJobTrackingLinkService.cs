namespace BaggageDelivery.Core.Interfaces;

public interface IJobTrackingLinkService
{
    Task<string?> GetTrackingUrlAsync(int jobId, CancellationToken ct);
}
