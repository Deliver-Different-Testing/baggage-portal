using BaggageDelivery.Core.Http.Models;
using BaggageDelivery.Core.Interfaces;
using Serilog;

namespace BaggageDelivery.Core.Services;

// No BagDelBooking shadow row anymore — tucJob holds the canonical state.
// GetSummary reads from trackingpage (anonymous read of the Despatch DB);
// Confirm writes through the api repo (SC-JWT, never direct to tucJob).
// Idempotency comes from the api side: re-confirming a job already at
// Dispatched is a no-op.
internal sealed class PaxBookingService(
    ITrackingPageClient trackingPage,
    IDespatchApiClient despatch,
    TimeProvider time) : IPaxBookingService
{
    public async Task<BookingSummary?> GetSummaryAsync(int jobId, CancellationToken ct)
    {
        var tracking = await trackingPage.GetJobAsync(jobId, ct);
        if (tracking is null)
        {
            Log.Warning("GetSummary: trackingpage returned no data for job {JobId}", jobId);
            return null;
        }

        var now = time.GetUtcNow().UtcDateTime;
        return new BookingSummary(
            JobId: jobId,
            Reference: tracking.JobId.ToString(),
            AirlineLabel: tracking.CourierFirstName ?? "Your Airline",
            PassengerName: string.Empty,
            PassengerPhone: null,
            PassengerEmail: null,
            // Pax-facing summary doesn't carry a delivery address yet —
            // trackingpage doesn't expose tucJob.DeliveryAddressLine* in
            // its JobFullDto. Address surfaces after the api delivery
            // patch lands when the pax submits the form.
            DeliveryAddress: new AddressUpdateDto
            {
                Line1 = string.Empty,
                City = string.Empty,
                Country = "NZ"
            },
            EarliestSlotUtc: tracking.EtaWindowStartUtc ?? now,
            LatestSlotUtc: tracking.EtaWindowEndUtc ?? now.AddDays(2));
    }

    public async Task ConfirmAsync(ConfirmBookingInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);

        var deliveryUpdate = new DeliveryUpdateRequest
        {
            Address = input.Address,
            TimeSlotStartUtc = input.TimeSlotStartUtc,
            TimeSlotEndUtc = input.TimeSlotEndUtc,
            AtlOption = input.AtlOption,
            AccessNotes = input.AccessNotes,
            PhoneOverride = input.PhoneOverride
        };

        var updateOk = await despatch.UpdateJobDeliveryAsync(input.JobId, deliveryUpdate, ct);
        if (!updateOk)
        {
            throw new InvalidOperationException(
                $"api Jobs/{input.JobId}/delivery returned non-success — pax confirmation not persisted");
        }

        var releaseOk = await despatch.ReleaseBaggageJobAsync(
            new BookingReleaseRequest { JobID = input.JobId }, ct);
        if (!releaseOk)
        {
            throw new InvalidOperationException(
                $"api Baggage/release returned non-success for JobId={input.JobId} — pax confirmation not persisted");
        }

        Log.Information("Pax confirmation released: JobId={JobId}", input.JobId);
    }
}
