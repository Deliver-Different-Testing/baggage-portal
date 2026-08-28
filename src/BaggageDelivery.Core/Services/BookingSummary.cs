using BaggageDelivery.Core.Http.Models;

namespace BaggageDelivery.Core.Services;

public sealed record BookingSummary(
    int JobId,
    string FileReference,
    string AirlineLabel,
    string? AirlineCode,
    string SupportPhone,
    string PassengerName,
    string? PassengerPhone,
    string? PassengerEmail,
    AddressUpdateDto DeliveryAddress,
    DateTime EarliestSlotUtc,
    DateTime LatestSlotUtc,
    IReadOnlyList<AtlOptionDto> AtlOptions,
    int? DefaultAtlOptionId);
