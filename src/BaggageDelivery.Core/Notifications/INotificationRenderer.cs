namespace BaggageDelivery.Core.Notifications;

public interface INotificationRenderer
{
    // Renders the magic-link message body for the chosen channel. Source templates
    // are MJML (email) and plain text with placeholders (SMS).
    Task<RenderedNotification> RenderMagicLinkAsync(MagicLinkRenderContext context, CancellationToken ct);
}

public sealed record MagicLinkRenderContext(
    string Channel,
    string PassengerName,
    string AirlineLabel,
    string Reference,
    string MagicLinkUrl);

public sealed record RenderedNotification(string Subject, string Body);
