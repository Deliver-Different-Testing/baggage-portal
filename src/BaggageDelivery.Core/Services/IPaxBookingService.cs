using BaggageDelivery.Core.Http.Models;

namespace BaggageDelivery.Core.Services;

public interface IPaxBookingService
{
    // Returns the booking summary for a verified job (read-only).
    Task<BookingSummary?> GetSummaryAsync(int jobId, int tenantId, CancellationToken ct);

    // Persists the passenger's confirmation and queues an outbox row.
    // Throws ConfirmationAlreadyExistsException if the token has already been used.
    Task<int> ConfirmAsync(ConfirmBookingInput input, CancellationToken ct);
}

public sealed record BookingSummary(
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
    int JobId,
    int TenantId,
    int TokenId,
    AddressUpdateDto Address,
    DateTime TimeSlotStartUtc,
    DateTime TimeSlotEndUtc,
    string AtlOption,
    string? AccessNotes,
    string? PhoneOverride);

public sealed class ConfirmationAlreadyExistsException(int jobId)
    : Exception($"Job {jobId} already has a confirmation");
