namespace BaggageDelivery.Core.Http.Models;

public sealed class AddressUpdateDto
{
    public string? Line1 { get; init; }
    public string? Line2 { get; init; }
    public string? Line3 { get; init; }
    public required string Line4 { get; init; }
    public required string Line5 { get; init; }
    public required string Line6 { get; init; }
    public string? Line7 { get; init; }
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
