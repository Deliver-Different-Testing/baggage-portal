namespace BaggageDelivery.Core.Enums;

// String values used in JobDeliveryJourney.ChangeType. Mirror of
// DespatchWeb.Enums.DeliveryJourneyChangeType — kept in sync so the
// dispatcher timeline reads BaggageDelivery's audit rows the same way it
// reads despatchweb's. Use nameof(...) when stamping the column.
public enum DeliveryJourneyChangeType
{
    InternalStatus,
    JobStatus,
    FlightAssignment,
    AgentAssignment,
    JobUpdate,
    CourierAssignment
}

public enum DeliveryJourneyUpdatedByType
{
    Staff,
    Courier,
    System
}
