namespace BaggageDelivery.Core.AddressLookup;

public sealed class AddressDetail
{
    public string Street { get; init; } = string.Empty;
    public string Suburb { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string StateCode { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
    public string CountryCode { get; init; } = string.Empty;
}