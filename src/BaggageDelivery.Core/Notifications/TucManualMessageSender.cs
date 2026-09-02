using BaggageDelivery.Core.Interfaces;
using BaggageDelivery.Core.Models;

namespace BaggageDelivery.Core.Notifications;

internal sealed class TucManualMessageSender(
    BaggageDeliveryContext db,
    INotificationRenderer renderer,
    TimeProvider time) : INotificationService
{
    public async Task SendBookingLinkAsync(int jobId, string recipient, BookingNotificationContext context, CancellationToken ct)
    {
        var rendered = await renderer.RenderBookingLinkAsync(context, ct);
        await EnqueueAsync(jobId, recipient, context.Channel, rendered, ct);
    }

    public async Task SendBookingConfirmedAsync(
        int jobId, string recipient, BookingConfirmedNotificationContext context, CancellationToken ct)
    {
        var rendered = await renderer.RenderBookingConfirmedAsync(context, ct);
        await EnqueueAsync(jobId, recipient, context.Channel, rendered, ct);
    }

    private async Task EnqueueAsync(
        int jobId, string recipient, string channel, RenderedNotification rendered, CancellationToken ct)
    {
        var entity = new TucManualMessage
        {
            UcmmDate = time.GetUtcNow().UtcDateTime,
            UcmmMessage = rendered.Body,
            Subject = rendered.Subject,
            JobId = jobId,
            UcmmAttempts = 0,
            UcmmSent = false,
            Read = false
        };

        switch (channel)
        {
            case NotificationChannel.Sms:
                entity.SendToMobile = recipient;
                break;
            case NotificationChannel.Email:
                entity.SendToEmailAddress = recipient;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(channel),
                    $"Unknown channel '{channel}'");
        }

        await db.TucManualMessages.AddAsync(entity, ct);
        await db.SaveChangesAsync(ct);
    }
}
