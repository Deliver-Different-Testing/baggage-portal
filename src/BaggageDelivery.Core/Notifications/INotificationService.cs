namespace BaggageDelivery.Core.Notifications;

public interface INotificationService
{
    Task<NotificationResult> SendAsync(NotificationMessage message, CancellationToken ct);
}

public sealed record NotificationMessage(
    string Channel,
    string Recipient,
    string Subject,
    string Body);

public sealed record NotificationResult(bool Sent, string? ProviderMessageId, string? Error);

public interface ISmsSender
{
    Task<NotificationResult> SendAsync(string toNumber, string body, CancellationToken ct);
}

public interface IEmailSender
{
    Task<NotificationResult> SendAsync(string toAddress, string subject, string htmlBody, CancellationToken ct);
}
