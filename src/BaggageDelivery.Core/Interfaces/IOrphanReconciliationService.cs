namespace BaggageDelivery.Core.Interfaces;

public interface IOrphanReconciliationService
{
    Task ReconcileOnceAsync(int batchSize, CancellationToken ct);
}
