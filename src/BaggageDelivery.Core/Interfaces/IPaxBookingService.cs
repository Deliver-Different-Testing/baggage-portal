using BaggageDelivery.Core.Services;

namespace BaggageDelivery.Core.Interfaces;

public interface IPaxBookingService
{
    // Loads the passenger-facing summary from trackingpage. Null when the
    // job isn't found (stale link).
    Task<BookingSummary?> GetSummaryAsync(int jobId, CancellationToken ct);

    // The job's Urgent number (tucJob.ucjbNumber) on its own, for the admin mint
    // path: the booking-link SMS/email has to quote the same Booking Reference the
    // portal shows. Null when Despatch has no such job — JobId is a soft reference.
    Task<string?> GetJobNumberAsync(int jobId, CancellationToken ct);

    // Builds the next eight delivery windows from the client's economy runs
    // (tucClient.EconomyRun1..8), rolling forward across business days until
    // there are eight. The run columns store time-of-day in tenant local time;
    // each window ends at the run plus the duration of the job's speed
    // (tucJobType.Minutes). Anchored on tenant-local today, or `localDate` if
    // supplied and still in the future. Clients not on run-based delivery fall
    // back to the global tblEcoSetting BaggageCutOff/BaggageRebook pair.
    Task<IReadOnlyList<BookingTimeSlot>> GetTimeslotsAsync(int jobId, DateTime? localDate, CancellationToken ct);

    // Forwards the passenger's submitted delivery details to the api repo:
    // patches tucJob delivery columns, then flips the job through the
    // release flow. Throws on api failure — caller surfaces the error to
    // the passenger so they can retry. Idempotency lives on the api side
    // (release is a no-op once the job is past On Hold).
    Task ConfirmAsync(ConfirmBookingInput input, CancellationToken ct);
}
