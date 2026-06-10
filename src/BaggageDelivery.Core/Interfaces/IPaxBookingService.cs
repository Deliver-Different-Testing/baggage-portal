using BaggageDelivery.Core.Services;

namespace BaggageDelivery.Core.Interfaces;

public interface IPaxBookingService
{
    // Loads the passenger-facing summary from trackingpage. Null when the
    // job isn't found (stale link).
    Task<BookingSummary?> GetSummaryAsync(int jobId, CancellationToken ct);

    // Forwards the passenger's submitted delivery details to the api repo:
    // patches tucJob delivery columns, then flips the job through the
    // release flow. Throws on api failure — caller surfaces the error to
    // the passenger so they can retry. Idempotency lives on the api side
    // (release is a no-op once the job is past On Hold).
    Task ConfirmAsync(ConfirmBookingInput input, CancellationToken ct);
}
