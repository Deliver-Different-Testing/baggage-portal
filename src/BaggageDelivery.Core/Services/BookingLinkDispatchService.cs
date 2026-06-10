using System.Text.Json;
using BaggageDelivery.Core.Interfaces;
using BaggageDelivery.Core.Models;
using BaggageDelivery.Core.Notifications;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace BaggageDelivery.Core.Services;

internal sealed class BookingLinkDispatchService(
    BaggageDeliveryContext db,
    INotificationRenderer renderer,
    INotificationService notifications,
    TimeProvider time) : IBookingLinkDispatchService
{
    private const int MaxAttempts = 5;

    public async Task<int> EnqueueAsync(EnqueueNotificationRequest request, CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var entity = new BagDelNotificationLog
        {
            JobId = request.JobId,
            Channel = request.Channel,
            Recipient = request.Recipient,
            Status = NotificationStatus.Pending,
            AttemptCount = 0,
            NextAttemptUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            // Render context packed into LastError for retry-stability; production
            // would use a dedicated payload column. v1 keeps the schema minimal.
            LastError = JsonSerializer.Serialize(new RenderPayload(
                request.PassengerName, request.AirlineLabel,
                request.Reference, request.BookingUrl))
        };
        db.BagDelNotificationLogs.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity.Id;
    }

    public async Task DrainOnceAsync(int batchSize, CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var rows = await db.BagDelNotificationLogs
            .AsTracking()
            .Where(n => n.Status == NotificationStatus.Pending && n.NextAttemptUtc <= now)
            .OrderBy(n => n.NextAttemptUtc)
            .Take(batchSize)
            .ToListAsync(ct);

        foreach (var row in rows)
        {
            await ProcessOneAsync(row, ct);
        }
    }

    private async Task ProcessOneAsync(BagDelNotificationLog row, CancellationToken ct)
    {
        RenderPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<RenderPayload>(row.LastError ?? "");
        }
        catch (Exception ex)
        {
            row.Status = NotificationStatus.Failed;
            row.LastError = $"Bad render payload: {ex.Message}";
            row.UpdatedAtUtc = time.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(ct);
            return;
        }

        if (payload is null)
        {
            row.Status = NotificationStatus.Failed;
            row.LastError = "Empty render payload";
            row.UpdatedAtUtc = time.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(ct);
            return;
        }

        var rendered = await renderer.RenderBookingLinkAsync(
            new BookingNotificationContext(row.Channel, payload.PassengerName,
                payload.AirlineLabel, payload.Reference, payload.BookingUrl),
            ct);

        var result = await notifications.SendAsync(
            new NotificationMessage(row.Channel, row.Recipient, rendered.Subject, rendered.Body), ct);

        row.AttemptCount++;
        row.UpdatedAtUtc = time.GetUtcNow().UtcDateTime;

        if (result.Sent)
        {
            row.Status = NotificationStatus.Sent;
            row.ProviderMsgId = result.ProviderMessageId;
            row.LastError = null;
            Log.Information(
                "Notification sent: JobId={JobId} Channel={Channel} ProviderMsgId={ProviderMsgId}",
                row.JobId, row.Channel, row.ProviderMsgId);
        }
        else
        {
            row.LastError = result.Error;
            if (row.AttemptCount >= MaxAttempts)
            {
                row.Status = NotificationStatus.Failed;
                Log.Error(
                    "Notification FAILED after {Attempts}: JobId={JobId} Channel={Channel} Error={Error}",
                    row.AttemptCount, row.JobId, row.Channel, result.Error);
            }
            else
            {
                var backoff = Math.Min(900, (int)Math.Pow(2, row.AttemptCount) * 5);
                row.NextAttemptUtc = row.UpdatedAtUtc.AddSeconds(backoff);
            }
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another drainer beat us to this row — RowVersion mismatch.
            // Skip; future drain picks it up if still Pending.
            Log.Information(
                "Concurrent drain on NotificationLogId={Id}, skipping", row.Id);
        }
    }

    private sealed record RenderPayload(string PassengerName, string AirlineLabel, string Reference, string BookingUrl);
}
