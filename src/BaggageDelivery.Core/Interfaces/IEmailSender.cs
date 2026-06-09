using BaggageDelivery.Core.Notifications;

namespace BaggageDelivery.Core.Interfaces;

public interface IEmailSender
{
    Task<NotificationResult> SendAsync(string toAddress, string subject, string htmlBody, CancellationToken ct);
}
