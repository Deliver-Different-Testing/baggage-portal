using BaggageDelivery.Core.Http.Models;

namespace BaggageDelivery.Core.Services;

public sealed record BookingSummary(
    int JobId,
    string Reference,
    string AirlineLabel,
    string? AirlineCode,
    // The number behind "Need help? Call …": the client's own phone, or the
    // tenant's support line when the client has none. Empty means neither is
    // configured and the portal drops the line.
    string SupportPhone,
    string PassengerName,
    string? PassengerPhone,
    string? PassengerEmail,
    AddressUpdateDto DeliveryAddress,
    DateTime EarliestSlotUtc,
    DateTime LatestSlotUtc,
    IReadOnlyList<AtlOptionDto> AtlOptions);
