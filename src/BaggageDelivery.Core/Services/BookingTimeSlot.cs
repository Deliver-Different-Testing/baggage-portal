namespace BaggageDelivery.Core.Services;

public sealed record BookingTimeSlot(
    Guid Id,
    DateTime StartUtc,
    DateTime EndUtc,
    string Label,
    bool FirstAvailable);
