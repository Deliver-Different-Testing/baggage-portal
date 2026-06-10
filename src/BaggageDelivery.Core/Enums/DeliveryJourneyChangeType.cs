namespace BaggageDelivery.Core.Enums;

public enum DeliveryJourneyChangeType
{
    InternalStatus,
    JobStatus,
    FlightAssignment,
    AgentAssignment,
    JobUpdate,
    CourierAssignment,
    BaggageDeliveryBooking
}

public enum DeliveryJourneyUpdatedByType
{
    Staff,
    Courier,
    System
}
