namespace BaggageDelivery.Core.Services;

public sealed class BookingNotFoundException(int bookingId)
    : Exception($"Booking {bookingId} not found");
