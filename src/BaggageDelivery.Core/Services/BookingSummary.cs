using BaggageDelivery.Core.Http.Models;

namespace BaggageDelivery.Core.Services;

public sealed record BookingSummary(
    int JobId,
    // The Urgent job number (tucJob.ucjbNumber) — the reference ops and the
    // passenger both quote. Empty when the job carries none.
    string JobNumber,
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
    IReadOnlyList<AtlOptionDto> AtlOptions,
    // Which of AtlOptions the confirm page arrives with selected. Resolved here
    // rather than in the portal so the passenger flow never has to know a
    // LeaveNotHomeId or match on a display name. Null when the tenant offers
    // nothing to leave a bag with.
    int? DefaultAtlOptionId);
