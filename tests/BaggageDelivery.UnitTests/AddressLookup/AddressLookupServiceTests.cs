using System.Net;
using BaggageDelivery.Core.AddressLookup;
using BaggageDelivery.UnitTests.Helpers;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace BaggageDelivery.UnitTests.AddressLookup;

public class AddressLookupServiceTests
{
    private static AddressLookupService NewService(StubHttpMessageHandler handler, string apiKey = "test-key")
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("HereMaps").Returns(handler.ToClient());

        return new AddressLookupService(factory, Options.Create(new HereMapsOptions { ApiKey = apiKey }));
    }

    private static string DecodedUrl(StubHttpMessageHandler handler) =>
        Uri.UnescapeDataString(handler.LastUrl?.ToString() ?? string.Empty);

    [Fact]
    public async Task Autocomplete_without_api_key_returns_empty_without_calling_HereMaps()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, """{ "items": [] }""");

        var results = await NewService(handler, apiKey: "")
            .AutocompleteAsync("queen st", null, TestContext.Current.CancellationToken);

        Assert.Empty(results);
        Assert.Empty(handler.RequestedUrls);
    }

    [Fact]
    public async Task Lookup_without_api_key_returns_null_without_calling_HereMaps()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, "{}");

        var detail = await NewService(handler, apiKey: "")
            .LookupAsync("here:af:street:xyz", TestContext.Current.CancellationToken);

        Assert.Null(detail);
        Assert.Empty(handler.RequestedUrls);
    }

    [Fact]
    public async Task Autocomplete_defaults_to_NZ_when_no_countries_configured()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, """{ "items": [] }""");

        await NewService(handler).AutocompleteAsync("queen st", null, TestContext.Current.CancellationToken);

        var url = DecodedUrl(handler);
        Assert.Contains("at=-40.900557,174.885971", url);
        Assert.Contains("in=countryCode:NZL", url);
    }

    [Fact]
    public async Task Autocomplete_defaults_to_NZ_when_countries_is_empty()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, """{ "items": [] }""");

        await NewService(handler).AutocompleteAsync("queen st", [], TestContext.Current.CancellationToken);

        Assert.Contains("in=countryCode:NZL", DecodedUrl(handler));
    }

    [Fact]
    public async Task Autocomplete_biases_on_the_first_country_and_filters_on_all()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, """{ "items": [] }""");

        await NewService(handler)
            .AutocompleteAsync("queen st", ["AU", "NZ"], TestContext.Current.CancellationToken);

        var url = DecodedUrl(handler);
        Assert.Contains("at=-25.274398,133.775136", url);
        Assert.Contains("in=countryCode:AUS,NZL", url);
    }

    [Fact]
    public async Task Autocomplete_uses_default_coordinates_for_a_country_with_no_bias_point()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, """{ "items": [] }""");

        await NewService(handler).AutocompleteAsync("shibuya", ["JP"], TestContext.Current.CancellationToken);

        var url = DecodedUrl(handler);
        Assert.Contains("at=-40.900557,174.885971", url);
        Assert.Contains("in=countryCode:JPN", url);
    }

    [Fact]
    public async Task Autocomplete_drops_country_codes_that_do_not_resolve()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, """{ "items": [] }""");

        await NewService(handler)
            .AutocompleteAsync("queen st", ["NZ", "ZZ"], TestContext.Current.CancellationToken);

        Assert.Contains("in=countryCode:NZL", DecodedUrl(handler));
    }

    [Fact]
    public async Task Autocomplete_passes_the_search_text_and_a_limit()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, """{ "items": [] }""");

        await NewService(handler).AutocompleteAsync("queen st", null, TestContext.Current.CancellationToken);

        var url = DecodedUrl(handler);
        Assert.Contains("q=queen st", url);
        Assert.Contains("apiKey=test-key", url);
        Assert.Contains("limit=10", url);
    }

    [Fact]
    public async Task Autocomplete_non_success_returns_empty()
    {
        var handler = StubHttpMessageHandler.Text(HttpStatusCode.Forbidden, "quota exceeded");

        var results = await NewService(handler)
            .AutocompleteAsync("queen st", null, TestContext.Current.CancellationToken);

        Assert.Empty(results);
    }

    [Fact]
    public async Task Autocomplete_null_payload_returns_empty()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, "null");

        var results = await NewService(handler)
            .AutocompleteAsync("queen st", null, TestContext.Current.CancellationToken);

        Assert.Empty(results);
    }

    [Fact]
    public async Task Autocomplete_maps_an_item_and_composes_street_from_house_number()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, """
                                                                     {
                                                                       "items": [{
                                                                         "id": "here:af:street:abc",
                                                                         "title": "12 Queen Street, Auckland",
                                                                         "resultType": "houseNumber",
                                                                         "address": {
                                                                           "label": "12 Queen Street, Auckland 1010, New Zealand",
                                                                           "countryCode": "NZL",
                                                                           "state": "Auckland",
                                                                           "district": "Auckland Central",
                                                                           "city": "Auckland",
                                                                           "street": "Queen Street",
                                                                           "houseNumber": "12",
                                                                           "postalCode": "1010"
                                                                         }
                                                                       }]
                                                                     }
                                                                     """);

        var results = await NewService(handler)
            .AutocompleteAsync("queen st", null, TestContext.Current.CancellationToken);

        var result = Assert.Single(results);
        Assert.Equal("here:af:street:abc", result.Id);
        Assert.Equal("12 Queen Street, Auckland", result.Title);
        Assert.Equal("12 Queen Street", result.Street);
        Assert.Equal("Auckland Central", result.Suburb);
        Assert.Equal("Auckland", result.City);
        Assert.Equal("Auckland", result.State);
        Assert.Equal("1010", result.PostalCode);
        Assert.Equal("NZ", result.CountryCode);
    }

    [Fact]
    public async Task Autocomplete_omits_house_number_when_absent()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, """
                                                                     {
                                                                       "items": [{
                                                                         "id": "here:af:street:def",
                                                                         "title": "Queen Street",
                                                                         "resultType": "street",
                                                                         "address": {
                                                                           "label": "Queen Street, Auckland, New Zealand",
                                                                           "countryCode": "NZL",
                                                                           "street": "Queen Street",
                                                                           "houseNumber": ""
                                                                         }
                                                                       }]
                                                                     }
                                                                     """);

        var results = await NewService(handler)
            .AutocompleteAsync("queen st", null, TestContext.Current.CancellationToken);

        Assert.Equal("Queen Street", Assert.Single(results).Street);
    }

    [Fact]
    public async Task Autocomplete_emits_empty_country_for_an_unmappable_code()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, """
                                                                     {
                                                                       "items": [{
                                                                         "id": "x", "title": "Somewhere", "resultType": "street",
                                                                         "address": { "label": "Somewhere", "countryCode": "ZZZ", "street": "Somewhere" }
                                                                       }]
                                                                     }
                                                                     """);

        var results = await NewService(handler)
            .AutocompleteAsync("somewhere", null, TestContext.Current.CancellationToken);

        Assert.Equal(string.Empty, Assert.Single(results).CountryCode);
    }

    [Fact]
    public async Task Autocomplete_discards_category_and_chain_queries_and_unusable_addresses()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, """
                                                                     {
                                                                       "items": [
                                                                         { "id": "1", "title": "Cafes", "resultType": "categoryQuery",
                                                                           "address": { "label": "Cafes near me", "countryCode": "NZL", "street": "x" } },
                                                                         { "id": "2", "title": "Starbucks", "resultType": "chainQuery",
                                                                           "address": { "label": "Starbucks", "countryCode": "NZL", "street": "x" } },
                                                                         { "id": "3", "title": "No address", "resultType": "street", "address": null },
                                                                         { "id": "4", "title": "Blank label", "resultType": "street",
                                                                           "address": { "label": "   ", "countryCode": "NZL", "street": "x" } },
                                                                         { "id": "5", "title": "Keeper", "resultType": "houseNumber",
                                                                           "address": { "label": "1 Real Street", "countryCode": "NZL", "street": "Real Street", "houseNumber": "1" } }
                                                                       ]
                                                                     }
                                                                     """);

        var results = await NewService(handler)
            .AutocompleteAsync("queen st", null, TestContext.Current.CancellationToken);

        Assert.Equal("5", Assert.Single(results).Id);
    }

    [Fact]
    public async Task Lookup_requests_the_address_id_with_country_and_street_detail()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, """
                                                                     { "id": "here:af:street:abc", "address": { "label": "x", "countryCode": "NZL" } }
                                                                     """);

        await NewService(handler).LookupAsync("here:af:street:abc", TestContext.Current.CancellationToken);

        var url = DecodedUrl(handler);
        Assert.Contains("id=here:af:street:abc", url);
        Assert.Contains("show=countryInfo,streetInfo", url);
    }

    [Fact]
    public async Task Lookup_non_success_returns_null()
    {
        var handler = StubHttpMessageHandler.Text(HttpStatusCode.NotFound, "no such id");

        var detail = await NewService(handler)
            .LookupAsync("here:af:street:missing", TestContext.Current.CancellationToken);

        Assert.Null(detail);
    }

    [Fact]
    public async Task Lookup_without_an_address_returns_null()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK,
            """{ "id": "here:af:street:abc", "address": null }""");

        var detail = await NewService(handler)
            .LookupAsync("here:af:street:abc", TestContext.Current.CancellationToken);

        Assert.Null(detail);
    }

    [Fact]
    public async Task Lookup_null_payload_returns_null()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, "null");

        var detail = await NewService(handler)
            .LookupAsync("here:af:street:abc", TestContext.Current.CancellationToken);

        Assert.Null(detail);
    }

    [Fact]
    public async Task Lookup_maps_the_address_and_composes_street()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, """
                                                                     {
                                                                       "id": "here:af:street:abc",
                                                                       "address": {
                                                                         "label": "12 Queen Street, Auckland 1010, New Zealand",
                                                                         "countryCode": "NZL",
                                                                         "state": "Auckland",
                                                                         "stateCode": "AUK",
                                                                         "district": "Auckland Central",
                                                                         "city": "Auckland",
                                                                         "street": "Queen Street",
                                                                         "houseNumber": "12",
                                                                         "postalCode": "1010"
                                                                       }
                                                                     }
                                                                     """);

        var detail = await NewService(handler)
            .LookupAsync("here:af:street:abc", TestContext.Current.CancellationToken);

        Assert.NotNull(detail);
        Assert.Equal("12 Queen Street", detail.Street);
        Assert.Equal("Auckland Central", detail.Suburb);
        Assert.Equal("Auckland", detail.City);
        Assert.Equal("Auckland", detail.State);
        Assert.Equal("AUK", detail.StateCode);
        Assert.Equal("1010", detail.PostalCode);
        Assert.Equal("NZ", detail.CountryCode);
    }

    [Fact]
    public async Task Lookup_omits_house_number_when_absent()
    {
        var handler = StubHttpMessageHandler.Json(HttpStatusCode.OK, """
                                                                     {
                                                                       "id": "here:af:street:def",
                                                                       "address": { "label": "Queen Street", "countryCode": "NZL", "street": "Queen Street", "houseNumber": "" }
                                                                     }
                                                                     """);

        var detail = await NewService(handler)
            .LookupAsync("here:af:street:def", TestContext.Current.CancellationToken);

        Assert.Equal("Queen Street", detail?.Street);
    }
}