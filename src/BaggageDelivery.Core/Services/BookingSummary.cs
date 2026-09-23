using BaggageDelivery.Core.Http.Models;

namespace BaggageDelivery.Core.Services;

public sealed record BookingSummary(
    int JobId,
    string JobNumber,
    string FileReference,
    string AirlineLabel,
    string AirlineSmsName,
    string? AirlineCode,
    string SupportPhone,
    string PassengerName,
    string? PassengerPhone,
    string? PassengerEmail,
    AddressUpdateDto DeliveryAddress,
    IReadOnlyList<string> DeliveryNotes,
    DateTime EarliestSlotUtc,
    DateTime LatestSlotUtc,
    IReadOnlyList<AtlOptionDto> AtlOptions,
    int? DefaultAtlOptionId,
    bool TrackingAvailable,
    int BookingLeadTimeMinutes,
    BookingConfirmation? Confirmation);

public sealed record BookingConfirmation(
    DateTime ConfirmedAtUtc,
    DateTime? DeliveryTimeUtc,
    string DayLabel,
    string WindowLabel,
    int? AtlOptionId,
    string? AccessNotes,
    DateTime? EditableUntilUtc,
    bool CanEdit);
