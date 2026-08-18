using BaggageDelivery.Core.AddressLookup;

namespace BaggageDelivery.Core.Interfaces;

public interface IAddressLookupService
{
    Task<IReadOnlyList<AddressSearchResult>> AutocompleteAsync(
        string text,
        IReadOnlyList<string>? countryCodes,
        CancellationToken ct = default);

    Task<AddressDetail?> LookupAsync(string addressId, CancellationToken ct = default);
}