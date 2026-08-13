namespace BaggageDelivery.Core.Services;

// A run is a single departure time, not a window — Despatch has no closing
// edge for one. See UTL_fncJob_GetNextAvailableEconomyRun_DateTime.
public sealed record BookingTimeSlot(
    Guid Id,
    DateTime RunUtc,
    string Label,
    bool FirstAvailable);
