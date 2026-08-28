using System.Collections.Frozen;
using System.Net.Http.Json;
using BaggageDelivery.Core.Globalization;
using BaggageDelivery.Core.Interfaces;
using Microsoft.Extensions.Options;
using Serilog;

namespace BaggageDelivery.Core.AddressLookup;

public sealed class AddressLookupService(
    IHttpClientFactory httpClientFactory,
    IOptions<HereMapsOptions> options)
    : IAddressLookupService
{
    private const string AutosuggestBaseUrl = "https://geocode.search.hereapi.com/v1/autosuggest";
    private const string LookupBaseUrl = "https://lookup.search.hereapi.com/v1/lookup";

    private static readonly FrozenDictionary<string, string> CountryCoordinates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["US"] = "37.09024,-95.712891",
        ["CA"] = "56.130366,-106.346771",
        ["GB"] = "55.378051,-3.435973",
        ["AU"] = "-25.274398,133.775136",
        ["NZ"] = "-40.900557,174.885971"
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private const string DefaultCountryCode = "NZ";
    private const string DefaultCoordinates = "-40.900557,174.885971";

    public async Task<IReadOnlyList<AddressSearchResult>> AutocompleteAsync(
        string text,
        IReadOnlyList<string>? countryCodes,
        CancellationToken ct = default)
    {
        var apiKey = options.Value.ApiKey;
        if (string.IsNullOrEmpty(apiKey))
        {
            Log.Warning("HereMaps API key is not configured");
            return [];
        }

        var client = httpClientFactory.CreateClient("HereMaps");

        var effectiveCountries = countryCodes is { Count: > 0 } ? countryCodes : [DefaultCountryCode];
        var primary = effectiveCountries[0];
        var at = CountryCoordinates.GetValueOrDefault(primary, DefaultCoordinates);
     
        var iso3List = string.Join(',', effectiveCountries
            .Select(CountryCodes.ToIso3)
            .Where(c => c is not null));

        var queryParams = new Dictionary<string, string>
        {
            ["q"] = text,
            ["apiKey"] = apiKey,
            ["at"] = at,
            ["in"] = $"countryCode:{iso3List}",
            ["limit"] = "10"
        };
        var queryString = string.Join("&", queryParams.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));
        var url = $"{AutosuggestBaseUrl}?{queryString}";

        var httpResponse = await client.GetAsync(url, ct);
        if (!httpResponse.IsSuccessStatusCode)
        {
            var body = await httpResponse.Content.ReadAsStringAsync(ct);
            Log.Warning("HereMaps autosuggest returned {StatusCode}: {Body}", (int)httpResponse.StatusCode, body);
            return [];
        }

        var response = await httpResponse.Content.ReadFromJsonAsync<HereMapsAutosuggestResponse>(ct);
        if (response?.Items is null)
        {
            return [];
        }

        string[] excludedTypes = ["categoryQuery", "chainQuery"];
        return
        [
            .. response.Items
                .Where(i => i.Address is not null
                            && !string.IsNullOrWhiteSpace(i.Address.Label)
                            && !excludedTypes.Contains(i.ResultType))
                .Select(i =>
                {
                    var addr = i.Address!;
                    var street = string.IsNullOrEmpty(addr.HouseNumber)
                        ? addr.Street
                        : $"{addr.HouseNumber} {addr.Street}";

                    return new AddressSearchResult
                    {
                        Id = i.Id,
                        Title = i.Title,
                        Street = street,
                        Suburb = addr.District,
                        City = addr.City,
                        State = addr.State,
                        PostalCode = addr.PostalCode,
                        CountryCode = ToIso2(addr.CountryCode)
                    };
                })
        ];
    }

    public async Task<AddressDetail?> LookupAsync(string addressId, CancellationToken ct = default)
    {
        var apiKey = options.Value.ApiKey;
        if (string.IsNullOrEmpty(apiKey))
        {
            Log.Warning("HereMaps API key is not configured");
            return null;
        }

        var client = httpClientFactory.CreateClient("HereMaps");

        var url = $"{LookupBaseUrl}?id={Uri.EscapeDataString(addressId)}&show=countryInfo,streetInfo&apiKey={apiKey}";

        var httpResponse = await client.GetAsync(url, ct);
        if (!httpResponse.IsSuccessStatusCode)
        {
            var body = await httpResponse.Content.ReadAsStringAsync(ct);
            Log.Warning("HereMaps lookup returned {StatusCode}: {Body}", (int)httpResponse.StatusCode, body);
            return null;
        }

        var response = await httpResponse.Content.ReadFromJsonAsync<HereMapsLookupResponse>(ct);
        if (response?.Address is null)
        {
            return null;
        }

        var addr = response.Address;
        var street = string.IsNullOrEmpty(addr.HouseNumber)
            ? addr.Street
            : $"{addr.HouseNumber} {addr.Street}";

        return new AddressDetail
        {
            Street = street,
            Suburb = addr.District,
            City = addr.City,
            State = addr.State,
            StateCode = addr.StateCode,
            PostalCode = addr.PostalCode,
            CountryCode = ToIso2(addr.CountryCode)
        };
    }

    private static string ToIso2(string countryCode) =>
        CountryCodes.TryToIso2(countryCode, out var iso2) ? iso2 : string.Empty;
}
