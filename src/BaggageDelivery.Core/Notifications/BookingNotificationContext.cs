namespace BaggageDelivery.Core.Notifications;

public sealed record BookingNotificationContext(
    string Channel,
    string PassengerName,
    string AirlineName,
    string? FileReference,
    string BookingUrl);
