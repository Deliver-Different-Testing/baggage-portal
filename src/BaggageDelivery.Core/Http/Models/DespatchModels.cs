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
