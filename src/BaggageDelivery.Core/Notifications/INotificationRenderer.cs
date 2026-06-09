namespace BaggageDelivery.Core.Notifications;

public interface INotificationRenderer
{
    Task<RenderedNotification> RenderBookingLinkAsync(BookingNotificationContext context, CancellationToken ct);
}

public sealed record BookingNotificationContext(
    string Channel,
    string PassengerName,
    string AirlineLabel,
    string Reference,
    string BookingUrl);

public sealed record RenderedNotification(string Subject, string Body);
