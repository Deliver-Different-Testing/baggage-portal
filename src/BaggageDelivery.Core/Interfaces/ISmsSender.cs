using BaggageDelivery.Core.Notifications;

namespace BaggageDelivery.Core.Interfaces;

public interface ISmsSender
{
    Task<NotificationResult> SendAsync(string toNumber, string body, CancellationToken ct);
}
