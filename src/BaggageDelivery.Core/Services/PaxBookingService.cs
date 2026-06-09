using BaggageDelivery.Core.Http.Models;
using BaggageDelivery.Core.Interfaces;
using BaggageDelivery.Core.Models;
using BaggageDelivery.Core.MultiTenant;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace BaggageDelivery.Core.Services;

internal sealed class PaxBookingService(
    BaggageDeliveryContext db,
    IDespatchApiClient despatch,
    ITenantResolver tenants,
    TimeProvider time) : IPaxBookingService
{
    public async Task<BookingSummary?> GetSummaryAsync(int tenantId, int jobId, CancellationToken ct)
    {
        TenantContext ctx;
        try
        {
            ctx = await tenants.ResolveAsync(tenantId, ct);
        }
        catch (InvalidOperationException ex)
        {
            // Tenant not configured — treat the link as stale so the passenger
            // sees /expired rather than a 500.
            Log.Warning(ex, "GetSummary: tenant {TenantId} is not configured", tenantId);
            return null;
        }

        var booking = await FindOrCreateAsync(tenantId, jobId, ct);
        if (booking is null)
        {
            return null;
        }

        var tracking = await despatch.GetJobTrackingAsync(
            tenantId, ctx.Connection, ctx.TimeZone, clientId: null, contactId: 0, jobId, ct);

        if (tracking is null)
        {
            Log.Warning("GetSummary: Despatch returned no tracking for job {JobId} — falling back to row-only summary", jobId);
        }

        var now = time.GetUtcNow().UtcDateTime;
        return new BookingSummary(
            BookingId: booking.Id,
            JobId: jobId,
            Reference: (tracking?.JobId ?? jobId).ToString(),
            AirlineLabel: tracking?.CourierFirstName ?? "Your Airline",
            PassengerName: string.Empty,
            PassengerPhone: null,
            PassengerEmail: null,
            DeliveryAddress: new AddressUpdateDto
            {
                Line1 = booking.AddressLine1 ?? string.Empty,
                Line2 = booking.AddressLine2,
                Suburb = booking.Suburb,
                City = booking.City ?? string.Empty,
                PostCode = booking.PostCode,
                Country = booking.Country ?? "NZ",
                Latitude = booking.Latitude,
                Longitude = booking.Longitude
            },
            EarliestSlotUtc: tracking?.EtaWindowStartUtc ?? now,
            LatestSlotUtc: tracking?.EtaWindowEndUtc ?? now.AddDays(2));
    }

    public async Task ConfirmAsync(ConfirmBookingInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);

        var booking = await FindOrCreateAsync(input.TenantId, input.JobId, ct)
            ?? throw new InvalidOperationException(
                $"Could not load or create BagDelBooking for (TenantId={input.TenantId}, JobId={input.JobId})");

        if (booking.ConfirmedAtUtc is not null)
        {
            throw new ConfirmationAlreadyExistsException(booking.Id);
        }

        var now = time.GetUtcNow().UtcDateTime;

        booking.ConfirmedAtUtc = now;
        booking.AddressLine1 = input.Address.Line1;
        booking.AddressLine2 = input.Address.Line2;
        booking.Suburb = input.Address.Suburb;
        booking.City = input.Address.City;
        booking.PostCode = input.Address.PostCode;
        booking.Country = input.Address.Country;
        booking.Latitude = input.Address.Latitude;
        booking.Longitude = input.Address.Longitude;
        booking.TimeSlotStartUtc = input.TimeSlotStartUtc;
        booking.TimeSlotEndUtc = input.TimeSlotEndUtc;
        booking.AtlOption = input.AtlOption;
        booking.AccessNotes = input.AccessNotes;
        booking.PhoneOverride = input.PhoneOverride;

        var outbox = new BagDelConfirmationOutbox
        {
            BookingId = booking.Id,
            JobId = booking.JobId,
            TenantId = booking.TenantId,
            Status = OutboxStatus.Pending,
            NextAttemptUtc = now,
            CreatedAtUtc = now
        };

        await db.BagDelConfirmationOutboxes.AddAsync(outbox, ct);
        await db.SaveChangesAsync(ct);

        Log.Information(
            "Pax confirmation persisted for BookingId={BookingId} JobId={JobId} OutboxId={OutboxId}",
            booking.Id, booking.JobId, outbox.Id);
    }

    // Find-or-create on (TenantId, JobId). A UNIQUE constraint on those columns
    // (see dbmigrationsv2 migration UQ_BagDelBooking_Tenant_Job) keeps concurrent
    // first-hits from creating duplicates — the second SaveChangesAsync raises a
    // DbUpdateException, after which we resolve by re-reading. Returns null if
    // the insert was rejected for any reason other than the race (e.g. an
    // upstream constraint we can't satisfy from the pax flow).
    private async Task<BagDelBooking?> FindOrCreateAsync(int tenantId, int jobId, CancellationToken ct)
    {
        var booking = await db.BagDelBookings
            .AsTracking()
            .FirstOrDefaultAsync(b => b.TenantId == tenantId && b.JobId == jobId, ct);

        if (booking is not null)
        {
            return booking;
        }

        booking = new BagDelBooking
        {
            JobId = jobId,
            TenantId = tenantId,
            CreatedAtUtc = time.GetUtcNow().UtcDateTime
        };
        db.BagDelBookings.Add(booking);

        try
        {
            await db.SaveChangesAsync(ct);
            return booking;
        }
        catch (DbUpdateException ex)
        {
            db.Entry(booking).State = EntityState.Detached;
            var existing = await db.BagDelBookings
                .AsTracking()
                .FirstOrDefaultAsync(b => b.TenantId == tenantId && b.JobId == jobId, ct);

            if (existing is not null)
            {
                return existing;
            }

            Log.Warning(ex,
                "FindOrCreate: failed to insert BagDelBooking for (TenantId={TenantId}, JobId={JobId})",
                tenantId, jobId);
            return null;
        }
    }
}
