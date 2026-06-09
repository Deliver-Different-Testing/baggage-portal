namespace BaggageDelivery.Core.Notifications;

public sealed record NotificationMessage(
    string Channel,
    string Recipient,
    string Subject,
    string Body);
