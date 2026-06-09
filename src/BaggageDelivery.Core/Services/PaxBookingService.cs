using BaggageDelivery.Core.Http.Models;
using BaggageDelivery.Core.Interfaces;
using BaggageDelivery.Core.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace BaggageDelivery.Core.Services;

internal sealed class PaxBookingService(
    BaggageDeliveryContext db,
    IDespatchApiClient despatch,
    ITenantResolver tenants,
    TimeProvider time) : IPaxBookingService
{
    public async Task<BookingSummary?> GetSummaryAsync(int bookingId, CancellationToken ct)
    {
        var booking = await db.BagDelBookings
            .AsNoTracking()
            .Where(b => b.Id == bookingId)
            .Select(b => new BookingSummaryProjection(
                b.Id, b.JobId, b.TenantId,
                b.AddressLine1, b.AddressLine2, b.Suburb, b.City, b.PostCode, b.Country,
                b.Latitude, b.Longitude))
            .FirstOrDefaultAsync(ct);

        if (booking is null)
        {
            return null;
        }

        var ctx = await tenants.ResolveAsync(booking.TenantId, ct);
        var tracking = await despatch.GetJobTrackingAsync(
            booking.TenantId, ctx.Connection, ctx.TimeZone, clientId: null, contactId: 0, booking.JobId, ct);

        if (tracking is null)
        {
            Log.Warning("GetSummary: Despatch returned no tracking for job {JobId}", booking.JobId);
            return null;
        }

        return new BookingSummary(
            BookingId: booking.Id,
            JobId: booking.JobId,
            Reference: tracking.JobId.ToString(),
            AirlineLabel: tracking.CourierFirstName ?? "Your Airline",
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
            EarliestSlotUtc: tracking.EtaWindowStartUtc ?? time.GetUtcNow().UtcDateTime,
            LatestSlotUtc: tracking.EtaWindowEndUtc ?? time.GetUtcNow().UtcDateTime.AddDays(2));
    }

    private sealed record BookingSummaryProjection(
        int Id, int JobId, int TenantId,
        string? AddressLine1, string? AddressLine2, string? Suburb, string? City, string? PostCode, string? Country,
        decimal? Latitude, decimal? Longitude);

    public async Task ConfirmAsync(ConfirmBookingInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);

        var booking = await db.BagDelBookings.AsTracking().FirstOrDefaultAsync(b => b.Id == input.BookingId, ct);
        if (booking is null)
        {
            throw new BookingNotFoundException(input.BookingId);
        }

        if (booking.ConfirmedAtUtc is not null)
        {
            throw new ConfirmationAlreadyExistsException(input.BookingId);
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
}
