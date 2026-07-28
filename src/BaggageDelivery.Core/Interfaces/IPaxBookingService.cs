using BaggageDelivery.Core.Services;

namespace BaggageDelivery.Core.Interfaces;

public interface IPaxBookingService
{
    // Loads the passenger-facing summary from trackingpage. Null when the
    // job isn't found (stale link).
    Task<BookingSummary?> GetSummaryAsync(int jobId, CancellationToken ct);

    // Builds the delivery time windows from the first tblEcoSetting row:
    // EconomyRun1→EconomyRun2, EconomyRun2→EconomyRun3, etc. The run
    // columns store time-of-day in tenant local time; we anchor to today
    // (or `localDate` if supplied) in the tenant timezone and emit UTC.
    Task<IReadOnlyList<BookingTimeSlot>> GetTimeslotsAsync(DateTime? localDate, CancellationToken ct);

    // Forwards the passenger's submitted delivery details to the api repo:
    // patches tucJob delivery columns, then flips the job through the
    // release flow. Throws on api failure — caller surfaces the error to
    // the passenger so they can retry. Idempotency lives on the api side
    // (release is a no-op once the job is past On Hold).
    Task ConfirmAsync(ConfirmBookingInput input, CancellationToken ct);
}
