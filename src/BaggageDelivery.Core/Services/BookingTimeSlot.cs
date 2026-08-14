namespace BaggageDelivery.Core.Services;

// RunUtc is the departure (see UTL_fncJob_GetNextAvailableEconomyRun_DateTime);
// Despatch has no closing edge for a run, so the window the passenger is promised
// ends at RunUtc + tucJobType.Minutes for the job's speed.
//
// DayLabel and Label are rendered server-side in the tenant's timezone on purpose:
// they are wall-clock times, and a browser in another zone re-deriving them from
// RunUtc would show a passenger in Sydney a different window than the one the
// driver is running.
public sealed record BookingTimeSlot(
    Guid Id,
    DateTime RunUtc,
    string DayLabel,
    string Label,
    bool FirstAvailable);
