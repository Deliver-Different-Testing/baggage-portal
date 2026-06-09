using BaggageDelivery.Core.Http;
using BaggageDelivery.Core.Http.Models;
using BaggageDelivery.Core.Models;
using BaggageDelivery.Core.Models.Entities;
using BaggageDelivery.Core.MultiTenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BaggageDelivery.Core.Services;

// Drains BagDelConfirmationOutbox: for each Pending row, updates the Despatch
// job's delivery details and flips it from ON HOLD to READY FOR DISPATCH.
// Exponential backoff with attempt cap; permanent failure transitions to Failed.
internal sealed class DespatchJobReleaseService(
    BaggageDeliveryContext db,
    IDespatchApiClient despatch,
    ITenantResolver tenants,
    TimeProvider time,
    ILogger<DespatchJobReleaseService> logger) : IDespatchJobReleaseService
{
    private const int MaxAttempts = 8;

    public async Task DrainOnceAsync(int batchSize, CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var rows = await db.ConfirmationOutbox
            .AsTracking()
            .Where(o => o.Status == OutboxStatus.Pending && o.NextAttemptUtc <= now)
            .OrderBy(o => o.NextAttemptUtc)
            .Take(batchSize)
            .ToListAsync(ct);

        foreach (var row in rows)
        {
            await ProcessOneAsync(row, ct);
        }
    }

    private async Task ProcessOneAsync(BagDelConfirmationOutbox row, CancellationToken ct)
    {
        var confirmation = await db.BookingConfirmations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == row.ConfirmationId, ct);

        if (confirmation is null)
        {
            row.Status = OutboxStatus.Failed;
            row.LastError = $"ConfirmationId {row.ConfirmationId} not found";
            await db.SaveChangesAsync(ct);
            return;
        }

        try
        {
            var tenant = await tenants.ResolveAsync(row.TenantId, ct);

            var deliveryUpdate = new DeliveryUpdateRequest
            {
                Address = new AddressUpdateDto
                {
                    Line1 = confirmation.AddressLine1,
                    Line2 = confirmation.AddressLine2,
                    Suburb = confirmation.Suburb,
                    City = confirmation.City,
                    PostCode = confirmation.PostCode,
                    Country = confirmation.Country,
                    Latitude = confirmation.Latitude,
                    Longitude = confirmation.Longitude
                },
                TimeSlotStartUtc = confirmation.TimeSlotStartUtc,
                TimeSlotEndUtc = confirmation.TimeSlotEndUtc,
                AtlOption = confirmation.AtlOption,
                AccessNotes = confirmation.AccessNotes,
                PhoneOverride = confirmation.PhoneOverride
            };

            var updateOk = await despatch.UpdateJobDeliveryAsync(
                tenant.TenantId, tenant.Connection, tenant.TimeZone,
                clientId: null, contactId: 0, row.JobId, deliveryUpdate, ct);

            if (!updateOk)
            {
                throw new InvalidOperationException("UpdateJobDeliveryAsync returned false");
            }

            var releaseOk = await despatch.ReleaseBaggageJobAsync(
                tenant.TenantId, tenant.Connection, tenant.TimeZone,
                clientId: null, contactId: 0,
                new BookingReleaseRequest { JobID = row.JobId },
                ct);

            if (!releaseOk)
            {
                throw new InvalidOperationException("ReleaseBaggageJobAsync returned false");
            }

            row.Status = OutboxStatus.Done;
            row.LastError = null;

            await db.BookingConfirmations
                .Where(c => c.Id == row.ConfirmationId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.DespatchSyncedAtUtc, time.GetUtcNow().UtcDateTime)
                    .SetProperty(c => c.DespatchSyncError, (string?)null), ct);

            await db.SaveChangesAsync(ct);

            logger.LogInformation(
                "Outbox processed: JobId={JobId} OutboxId={OutboxId}", row.JobId, row.Id);
        }
        catch (Exception ex)
        {
            row.AttemptCount++;
            row.LastError = ex.Message;

            if (row.AttemptCount >= MaxAttempts)
            {
                row.Status = OutboxStatus.Failed;
                logger.LogError(ex,
                    "Outbox FAILED after {Attempts} attempts: JobId={JobId} OutboxId={OutboxId}",
                    row.AttemptCount, row.JobId, row.Id);
            }
            else
            {
                var backoffSeconds = Math.Min(900, (int)Math.Pow(2, row.AttemptCount) * 5);
                row.NextAttemptUtc = time.GetUtcNow().UtcDateTime.AddSeconds(backoffSeconds);
                logger.LogWarning(ex,
                    "Outbox retrying: JobId={JobId} Attempt={Attempt} NextIn={BackoffSec}s",
                    row.JobId, row.AttemptCount, backoffSeconds);
            }

            await db.BookingConfirmations
                .Where(c => c.Id == row.ConfirmationId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.DespatchSyncError, ex.Message), ct);

            await db.SaveChangesAsync(ct);
        }
    }
}
