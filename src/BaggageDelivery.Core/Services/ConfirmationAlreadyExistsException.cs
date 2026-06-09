namespace BaggageDelivery.Core.Services;

public sealed class ConfirmationAlreadyExistsException(int bookingId)
    : Exception($"Booking {bookingId} already has a confirmation");
