using BaggageDelivery.Core.Notifications;

namespace BaggageDelivery.Core.Interfaces;

public interface INotificationService
{
    Task<NotificationResult> SendAsync(NotificationMessage message, CancellationToken ct);
}
