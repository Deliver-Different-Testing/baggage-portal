namespace BaggageDelivery.Core.Notifications;

public sealed record NotificationResult(bool Sent, string? ProviderMessageId, string? Error);
