namespace BaggageDelivery.Core.Services;

public interface IDespatchJobReleaseService
{
    Task DrainOnceAsync(int batchSize, CancellationToken ct);
}
