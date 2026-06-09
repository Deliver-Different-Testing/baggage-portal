using BaggageDelivery.Core.Services;

namespace BaggageDelivery.Core.Interfaces;

public interface IPaxBookingService
{
    // Loads (or lazily creates) the BagDelBooking shadow row for (tenantId, jobId)
    // and returns a passenger-facing summary. Returns null only when (tenantId,
    // jobId) does not resolve to a known tenant.
    Task<BookingSummary?> GetSummaryAsync(int tenantId, int jobId, CancellationToken ct);

    // Updates (or lazily creates) the BagDelBooking row for (TenantId, JobId) on
    // input and writes the passenger's submitted address / time slot / ATL choice,
    // stamping ConfirmedAtUtc. Throws ConfirmationAlreadyExistsException if the row
    // already has ConfirmedAtUtc set.
    Task ConfirmAsync(ConfirmBookingInput input, CancellationToken ct);
}
