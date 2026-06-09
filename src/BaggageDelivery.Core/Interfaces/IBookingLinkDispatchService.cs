using BaggageDelivery.Core.Services;

namespace BaggageDelivery.Core.Interfaces;

public interface IBookingLinkDispatchService
{
    Task<int> EnqueueAsync(EnqueueNotificationRequest request, CancellationToken ct);

    Task DrainOnceAsync(int batchSize, CancellationToken ct);
}
