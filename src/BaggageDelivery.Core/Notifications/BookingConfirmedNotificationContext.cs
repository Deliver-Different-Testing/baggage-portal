namespace BaggageDelivery.Core.Notifications;

public sealed record BookingConfirmedNotificationContext(
    string Channel,
    string PassengerName,
    string AirlineName,
    string? FileReference,
    string JobNumber,
    string DayLabel,
    string WindowLabel,
    string? TrackingUrl,
    bool IsUpdate = false);
