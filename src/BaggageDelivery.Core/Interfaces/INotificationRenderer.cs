using BaggageDelivery.Core.Notifications;

namespace BaggageDelivery.Core.Interfaces;

public interface INotificationRenderer
{
    Task<RenderedNotification> RenderBookingLinkAsync(BookingNotificationContext context, CancellationToken ct);

    Task<RenderedNotification> RenderBookingConfirmedAsync(
        BookingConfirmedNotificationContext context, CancellationToken ct);

    Task<RenderedNotification> RenderAddressUnserviceableAsync(
        AddressUnserviceableNotificationContext context, CancellationToken ct);
}
