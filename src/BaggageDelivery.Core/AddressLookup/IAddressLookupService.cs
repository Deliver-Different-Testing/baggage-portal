namespace BaggageDelivery.Core.AddressLookup;

public interface IAddressLookupService
{
    Task<IReadOnlyList<AddressSearchResult>> AutocompleteAsync(string text, string? countryCode, CancellationToken ct = default);
    Task<AddressDetail?> LookupAsync(string addressId, CancellationToken ct = default);
}

public sealed class AddressSearchResult
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Street { get; init; } = string.Empty;
    public string Suburb { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
    public string CountryCode { get; init; } = string.Empty;
}

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
