using BaggageDelivery.Core.Http.Models;

namespace BaggageDelivery.Core.Services;

public interface IPaxBookingService
{
    Task<BookingSummary?> GetSummaryAsync(int bookingId, CancellationToken ct);

    // Updates the BagDelBooking row in-place with the passenger's submitted address /
    // time slot / ATL choice and stamps ConfirmedAtUtc. Throws ConfirmationAlreadyExistsException
    // if the row already has ConfirmedAtUtc set.
    Task ConfirmAsync(ConfirmBookingInput input, CancellationToken ct);
}

public sealed record BookingSummary(
    int BookingId,
    int JobId,
    string Reference,
    string AirlineLabel,
    string PassengerName,
    string? PassengerPhone,
    string? PassengerEmail,
    AddressUpdateDto DeliveryAddress,
    DateTime EarliestSlotUtc,
    DateTime LatestSlotUtc);

public sealed record ConfirmBookingInput(
    int BookingId,
    AddressUpdateDto Address,
    DateTime TimeSlotStartUtc,
    DateTime TimeSlotEndUtc,
    string AtlOption,
    string? AccessNotes,
    string? PhoneOverride);

public sealed class ConfirmationAlreadyExistsException(int bookingId)
    : Exception($"Booking {bookingId} already has a confirmation");

public sealed class BookingNotFoundException(int bookingId)
    : Exception($"Booking {bookingId} not found");
