namespace BaggageDelivery.Core.Services;

public interface IBookingLinkDispatchService
{
    Task<int> EnqueueAsync(EnqueueNotificationRequest request, CancellationToken ct);

    Task DrainOnceAsync(int batchSize, CancellationToken ct);
}

public sealed record EnqueueNotificationRequest(
    int BookingId,
    string Channel,
    string Recipient,
    string PassengerName,
    string AirlineLabel,
    string Reference,
    string BookingUrl);
