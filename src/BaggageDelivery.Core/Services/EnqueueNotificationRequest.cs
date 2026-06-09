namespace BaggageDelivery.Core.Services;

public sealed record EnqueueNotificationRequest(
    int BookingId,
    string Channel,
    string Recipient,
    string PassengerName,
    string AirlineLabel,
    string Reference,
    string BookingUrl);
