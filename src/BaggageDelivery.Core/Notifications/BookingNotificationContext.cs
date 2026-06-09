namespace BaggageDelivery.Core.Notifications;

public sealed record BookingNotificationContext(
    string Channel,
    string PassengerName,
    string AirlineLabel,
    string Reference,
    string BookingUrl);
