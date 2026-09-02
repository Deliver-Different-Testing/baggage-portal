namespace BaggageDelivery.Core.Notifications;

public sealed record BookingConfirmedNotificationContext(
    string Channel,
    string PassengerName,
    string AirlineLabel,
    string? FileReference,
    string DayLabel,
    string WindowLabel,
    string? TrackingUrl);
