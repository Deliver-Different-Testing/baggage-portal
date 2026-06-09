using BaggageDelivery.Core.Http.Models;

namespace BaggageDelivery.Core.Services;

public sealed record ConfirmBookingInput(
    int TenantId,
    int JobId,
    AddressUpdateDto Address,
    DateTime TimeSlotStartUtc,
    DateTime TimeSlotEndUtc,
    string AtlOption,
    string? AccessNotes,
    string? PhoneOverride);
