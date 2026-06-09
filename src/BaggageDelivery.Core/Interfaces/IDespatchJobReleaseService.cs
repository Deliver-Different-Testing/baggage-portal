namespace BaggageDelivery.Core.Interfaces;

public interface IDespatchJobReleaseService
{
    Task DrainOnceAsync(int batchSize, CancellationToken ct);
}
