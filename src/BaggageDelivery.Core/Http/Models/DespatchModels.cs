namespace BaggageDelivery.Core.Http.Models;

// Shape mirrors WebAPICore.Models.Request.BookingReleaseRequest. Field names and
// casing must stay aligned with the Despatch API's expectations.
public sealed class BookingReleaseRequest
{
    public int JobID { get; set; }
    public int BookingID { get; set; }
}

public sealed class BookingCancelRequest
{
    public int ID { get; set; }
    public string JobNumber { get; set; } = "";
    public int JobID { get; set; }
    public string UserName { get; set; } = "";
}

public sealed class SendOnHoldBookingRequest
{
    public int JobID { get; set; }
    public int BookingID { get; set; }
}

// NEW endpoint (handoff to api repo) - PATCH api/Jobs/{jobId}/status
// Used on booking link mint to flip tucJob.UcjbStatus and write a
// JobDeliveryJourney audit row in the same transaction. tucJob is read-only
// from BaggageDelivery's SQL user — all tucJob writes route through the api.
public sealed class JobStatusUpdateRequest
{
    public int Status { get; set; }
    public string? Comment { get; set; }
}

// NEW endpoint (handoff to api repo) - PATCH api/Jobs/{jobId}/delivery
// PassengerName/Phone/Email map onto tucJob.DeliverToContact /
// DeliverToPhone / ProofOfDeliveryEmail on the api side. Name is required;
// the pax page forces a value before letting the submission through.
public sealed class DeliveryUpdateRequest
{
    public required AddressUpdateDto Address { get; init; }
    public required DateTime TimeSlotStartUtc { get; init; }
    public required DateTime TimeSlotEndUtc { get; init; }
    public required string AtlOption { get; init; }
    public string? AccessNotes { get; init; }
    public required string PassengerName { get; init; }
    public string? PassengerPhone { get; init; }
    public string? PassengerEmail { get; init; }
}

public sealed class AddressUpdateDto
{
    public required string Line1 { get; init; }
    public string? Line2 { get; init; }
    public string? Suburb { get; init; }
    public required string City { get; init; }
    public string? PostCode { get; init; }
    public required string Country { get; init; }
    public decimal? Latitude { get; init; }
    public decimal? Longitude { get; init; }
}

// NEW endpoint (handoff to api repo) - GET api/Jobs/{jobId}/tracking
public sealed class TrackingDto
{
    public required int JobId { get; init; }
    public required string CurrentStatus { get; init; }
    public required TrackingEventDto[] Events { get; init; }
    public DateTime? EtaWindowStartUtc { get; init; }
    public DateTime? EtaWindowEndUtc { get; init; }
    public string? CourierFirstName { get; init; }
    public string? VehicleLabel { get; init; }
}

public sealed class TrackingEventDto
{
    public required string Status { get; init; }
    public required DateTime AtUtc { get; init; }
    public string? LocationLabel { get; init; }
    public string? Description { get; init; }
}
