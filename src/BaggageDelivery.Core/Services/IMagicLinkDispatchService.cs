namespace BaggageDelivery.Core.Services;

public interface IMagicLinkDispatchService
{
    Task<int> EnqueueAsync(EnqueueNotificationRequest request, CancellationToken ct);

    Task DrainOnceAsync(int batchSize, CancellationToken ct);
}

public sealed record EnqueueNotificationRequest(
    int TokenId,
    string Channel,
    string Recipient,
    string PassengerName,
    string AirlineLabel,
    string Reference,
    string MagicLinkUrl);
