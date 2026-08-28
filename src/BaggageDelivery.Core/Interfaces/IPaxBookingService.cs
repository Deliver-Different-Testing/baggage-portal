using BaggageDelivery.Core.Services;

namespace BaggageDelivery.Core.Interfaces;

public interface IPaxBookingService
{
    Task<BookingSummary?> GetSummaryAsync(int jobId, CancellationToken ct);

    Task<BookingNotificationDetails?> GetNotificationDetailsAsync(int jobId, CancellationToken ct);

    Task<IReadOnlyList<BookingTimeSlot>> GetTimeslotsAsync(int jobId, DateTime? localDate, CancellationToken ct);

    Task ConfirmAsync(ConfirmBookingInput input, CancellationToken ct);
}
