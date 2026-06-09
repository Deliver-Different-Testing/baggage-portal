using BaggageDelivery.Core.Http;
using BaggageDelivery.Core.Http.Models;
using BaggageDelivery.Core.MagicLink;
using BaggageDelivery.Core.Models;
using BaggageDelivery.Core.Models.Entities;
using BaggageDelivery.Core.MultiTenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BaggageDelivery.Core.Services;

internal sealed class PaxBookingService(
    BaggageDeliveryContext db,
    IDespatchApiClient despatch,
    ITenantResolver tenants,
    IMagicLinkService magicLink,
    TimeProvider time,
    ILogger<PaxBookingService> logger) : IPaxBookingService
{
    // v1: pulls the booking summary from a stub the integration manager passes
    // alongside the magic-link mint. In a follow-up PR Despatch will expose
    // GET api/Jobs/{jobId}/summary so we can read directly.
    public async Task<BookingSummary?> GetSummaryAsync(int jobId, int tenantId, CancellationToken ct)
    {
        var ctx = await tenants.ResolveAsync(tenantId, ct);
        var tracking = await despatch.GetJobTrackingAsync(
            tenantId, ctx.Connection, ctx.TimeZone, clientId: null, contactId: 0, jobId, ct);

        if (tracking is null)
        {
            logger.LogWarning("GetSummary: Despatch returned no tracking for job {JobId}", jobId);
            return null;
        }

        // We deliberately do not include the existing confirmation here even if one exists -
        // the BookingSummary represents the BDO baseline. The PWA reads the active confirmation
        // (if any) separately to pre-fill the form.
        return new BookingSummary(
            JobId: jobId,
            Reference: tracking.JobId.ToString(),
            AirlineLabel: tracking.CourierFirstName ?? "Your Airline",
            PassengerName: "",
            PassengerPhone: null,
            PassengerEmail: null,
            DeliveryAddress: new AddressUpdateDto
            {
                Line1 = "",
                City = "",
                Country = "NZ"
            },
            EarliestSlotUtc: tracking.EtaWindowStartUtc ?? time.GetUtcNow().UtcDateTime,
            LatestSlotUtc: tracking.EtaWindowEndUtc ?? time.GetUtcNow().UtcDateTime.AddDays(2));
    }

    public async Task<int> ConfirmAsync(ConfirmBookingInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);

        var existing = await db.BookingConfirmations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.JobId == input.JobId, ct);

        if (existing is not null)
        {
            throw new ConfirmationAlreadyExistsException(input.JobId);
        }

        var now = time.GetUtcNow().UtcDateTime;

        var confirmation = new BagDelBookingConfirmation
        {
            JobId = input.JobId,
            TokenId = input.TokenId,
            ConfirmedAtUtc = now,
            AddressLine1 = input.Address.Line1,
            AddressLine2 = input.Address.Line2,
            Suburb = input.Address.Suburb,
            City = input.Address.City,
            PostCode = input.Address.PostCode,
            Country = input.Address.Country,
            Latitude = input.Address.Latitude,
            Longitude = input.Address.Longitude,
            TimeSlotStartUtc = input.TimeSlotStartUtc,
            TimeSlotEndUtc = input.TimeSlotEndUtc,
            AtlOption = input.AtlOption,
            AccessNotes = input.AccessNotes,
            PhoneOverride = input.PhoneOverride
        };
        db.BookingConfirmations.Add(confirmation);
        await db.SaveChangesAsync(ct);

        var outbox = new BagDelConfirmationOutbox
        {
            ConfirmationId = confirmation.Id,
            JobId = input.JobId,
            TenantId = input.TenantId,
            Status = OutboxStatus.Pending,
            NextAttemptUtc = now,
            CreatedAtUtc = now
        };
        db.ConfirmationOutbox.Add(outbox);
        await db.SaveChangesAsync(ct);

        await magicLink.MarkUsedAsync(input.TokenId, ct);

        logger.LogInformation(
            "Pax confirmation persisted for JobId={JobId} ConfirmationId={ConfirmationId} OutboxId={OutboxId}",
            input.JobId, confirmation.Id, outbox.Id);

        return confirmation.Id;
    }
}
