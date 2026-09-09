using BaggageDelivery.Core.Notifications;

namespace BaggageDelivery.Core.Interfaces;

public interface INotificationService
{
    Task SendBookingLinkAsync(int jobId, string recipient, BookingNotificationContext context, CancellationToken ct);

    Task SendBookingConfirmedAsync(
        int jobId, string recipient, BookingConfirmedNotificationContext context, CancellationToken ct);

    Task SendAddressUnserviceableAsync(
        int jobId, string recipient, AddressUnserviceableNotificationContext context, CancellationToken ct);
}
