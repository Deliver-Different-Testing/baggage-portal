namespace BaggageDelivery.Core.Services;

public sealed record EnqueueNotificationRequest(
    int JobId,
    string Channel,
    string Recipient,
    string PassengerName,
    string AirlineLabel,
    string Reference,
    string BookingUrl);
