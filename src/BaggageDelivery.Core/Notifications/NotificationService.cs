using BaggageDelivery.Core.Models;

namespace BaggageDelivery.Core.Notifications;

internal sealed class NotificationService(ISmsSender sms, IEmailSender email) : INotificationService
{
    public Task<NotificationResult> SendAsync(NotificationMessage message, CancellationToken ct)
    {
        return message.Channel switch
        {
            NotificationChannel.Sms => sms.SendAsync(message.Recipient, message.Body, ct),
            NotificationChannel.Email => email.SendAsync(message.Recipient, message.Subject, message.Body, ct),
            _ => Task.FromResult(new NotificationResult(false, null, $"Unknown channel '{message.Channel}'"))
        };
    }
}
