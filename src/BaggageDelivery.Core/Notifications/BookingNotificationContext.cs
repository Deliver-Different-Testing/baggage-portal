namespace BaggageDelivery.Core.Notifications;

public sealed record BookingNotificationContext(
    string Channel,
    string PassengerName,
    string AirlineLabel,
    string? FileReference,
    string BookingUrl);
