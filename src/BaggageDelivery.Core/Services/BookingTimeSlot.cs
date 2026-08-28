namespace BaggageDelivery.Core.Services;

public sealed record BookingTimeSlot(
    Guid Id,
    DateTime RunUtc,
    string DayLabel,
    string Label,
    bool FirstAvailable);
