using BaggageDelivery.Core.Services;

namespace BaggageDelivery.Core.Interfaces;

public interface IPaxBookingService
{
    Task<BookingSummary?> GetSummaryAsync(int bookingId, CancellationToken ct);

    // Updates the BagDelBooking row in-place with the passenger's submitted address /
    // time slot / ATL choice and stamps ConfirmedAtUtc. Throws ConfirmationAlreadyExistsException
    // if the row already has ConfirmedAtUtc set.
    Task ConfirmAsync(ConfirmBookingInput input, CancellationToken ct);
}
