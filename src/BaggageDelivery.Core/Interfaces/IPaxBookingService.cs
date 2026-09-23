using BaggageDelivery.Core.Http.Models;
using BaggageDelivery.Core.Services;

namespace BaggageDelivery.Core.Interfaces;

public interface IPaxBookingService
{
    Task<BookingSummary?> GetSummaryAsync(int jobId, CancellationToken ct);

    Task<BookingNotificationDetails?> GetNotificationDetailsAsync(int jobId, CancellationToken ct);

    Task<IReadOnlyList<BookingTimeSlot>> GetTimeslotsAsync(
        int jobId, DateTime? localDate, CancellationToken ct, int? jobTypeId = null, int? scheduleId = null);

    Task<IReadOnlyList<CandidateService>> GetAvailableServicesAsync(
        int jobId, AddressUpdateDto address, CancellationToken ct);

    Task ConfirmAsync(ConfirmBookingInput input, CancellationToken ct);

    Task AmendAsync(AmendBookingInput input, CancellationToken ct);

    Task<bool> RequestAirlineContactAsync(AddressContactRequestInput input, CancellationToken ct);
}
