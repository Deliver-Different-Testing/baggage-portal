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

        var entity = new TucManualMessage
        {
            UcmmDate = time.GetUtcNow().UtcDateTime,
            UcmmMessage = rendered.Body,
            Subject = rendered.Subject,
            JobId = jobId,
            UcmmAttempts = 0,
            UcmmSent = false,
            Read = false,
        };

        switch (context.Channel)
        {
            case NotificationChannel.Sms:
                entity.SendToMobile = recipient;
                break;
            case NotificationChannel.Email:
                entity.SendToEmailAddress = recipient;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(context),
                    $"Unknown channel '{context.Channel}'");
        }

        db.TucManualMessages.Add(entity);
        await db.SaveChangesAsync(ct);
    }
}
