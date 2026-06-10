namespace BaggageDelivery.Core.Http.Models;

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
