using System.Text.Json.Serialization;

namespace BaggageDelivery.Core.AddressLookup;

public sealed class HereMapsAutosuggestResponse
{
    [JsonPropertyName("items")]
    public List<HereMapsAutosuggestItem> Items { get; set; } = [];
}

public sealed class HereMapsAutosuggestItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("resultType")]
    public string ResultType { get; set; } = string.Empty;

    [JsonPropertyName("address")]
    public HereMapsAddress? Address { get; set; }
}

public sealed class HereMapsAddress
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("countryCode")]
    public string CountryCode { get; set; } = string.Empty;

    [JsonPropertyName("countryName")]
    public string CountryName { get; set; } = string.Empty;

    [JsonPropertyName("stateCode")]
    public string StateCode { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("district")]
    public string District { get; set; } = string.Empty;

    [JsonPropertyName("city")]
    public string City { get; set; } = string.Empty;

    [JsonPropertyName("street")]
    public string Street { get; set; } = string.Empty;

    [JsonPropertyName("houseNumber")]
    public string HouseNumber { get; set; } = string.Empty;

    [JsonPropertyName("postalCode")]
    public string PostalCode { get; set; } = string.Empty;
}

public sealed class HereMapsPosition
{
    [JsonPropertyName("lat")]
    public decimal? Lat { get; set; }

    [JsonPropertyName("lng")]
    public decimal? Lng { get; set; }
}

public sealed class HereMapsLookupResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("resultType")]
    public string ResultType { get; set; } = string.Empty;

    [JsonPropertyName("address")]
    public HereMapsAddress? Address { get; set; }

    [JsonPropertyName("position")]
    public HereMapsPosition? Position { get; set; }
}
