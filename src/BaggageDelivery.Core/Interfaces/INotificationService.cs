using BaggageDelivery.Core.Notifications;

namespace BaggageDelivery.Core.Interfaces;

public interface INotificationService
{
    Task SendBookingLinkAsync(int jobId, string recipient, BookingNotificationContext context, CancellationToken ct);

    Task SendBookingConfirmedAsync(
        int jobId, string recipient, BookingConfirmedNotificationContext context, CancellationToken ct);
}
