using BaggageDelivery.Api.Controllers.Pax;
using BaggageDelivery.Core.AddressLookup;
using BaggageDelivery.Core.Http;
using BaggageDelivery.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace BaggageDelivery.UnitTests.Controllers;

public class PaxAddressControllerTests
{
    private const string Token = "ENCRYPTED-TOKEN";

    private sealed class Harness
    {
        private IEncryptionService Encryption { get; } = Substitute.For<IEncryptionService>();
        public IAddressLookupService Lookup { get; } = Substitute.For<IAddressLookupService>();
        public PaxAddressController Controller { get; }

        public Harness(int? decryptsTo = 4242, string[]? countries = null)
        {
            Encryption.DecryptId(Arg.Any<string>()).Returns(decryptsTo);

            Controller = new PaxAddressController(
                Encryption,
                Lookup,
                Options.Create(new DespatchOptions { Countries = countries ?? ["NZ"] }));
        }
    }

    // ---- Autocomplete -------------------------------------------------------

    [Fact]
    public async Task Autocomplete_with_an_undecryptable_id_is_not_found()
    {
        var harness = new Harness(decryptsTo: null);

        var result = await harness.Controller.Autocomplete(
            Token, "queen st", TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(result.Result);
        await harness.Lookup.DidNotReceive().AutocompleteAsync(
            Arg.Any<string>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("q")]
    [InlineData("qu")]
    public async Task Autocomplete_below_three_characters_returns_empty_without_calling_HereMaps(string text)
    {
        var harness = new Harness();

        var result = await harness.Controller.Autocomplete(
            Token, text, TestContext.Current.CancellationToken);

        Assert.Empty(Assert.IsType<AddressSearchResult[]>(
            Assert.IsType<OkObjectResult>(result.Result).Value));
        await harness.Lookup.DidNotReceive().AutocompleteAsync(
            Arg.Any<string>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Autocomplete_passes_the_configured_countries_through()
    {
        var harness = new Harness(countries: ["AU", "NZ"]);
        var results = new List<AddressSearchResult> { new() { Id = "1", Title = "1 Queen St" } };
        harness.Lookup
            .AutocompleteAsync("queen st", Arg.Any<IReadOnlyList<string>?>(), Arg.Any<CancellationToken>())
            .Returns(results);

        var result = await harness.Controller.Autocomplete(
            Token, "queen st", TestContext.Current.CancellationToken);

        Assert.Same(results, Assert.IsType<OkObjectResult>(result.Result).Value);
        await harness.Lookup.Received(1).AutocompleteAsync(
            "queen st",
            Arg.Is<IReadOnlyList<string>?>(c => c != null && c.SequenceEqual(new[] { "AU", "NZ" })),
            Arg.Any<CancellationToken>());
    }

    // ---- Lookup -------------------------------------------------------------

    [Fact]
    public async Task Lookup_with_an_undecryptable_id_is_not_found()
    {
        var harness = new Harness(decryptsTo: null);

        var result = await harness.Controller.Lookup(
            Token, "here:af:street:abc", TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(result.Result);
        await harness.Lookup.DidNotReceive().LookupAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Lookup_returns_not_found_when_the_address_is_unknown()
    {
        var harness = new Harness();
        harness.Lookup.LookupAsync("here:af:street:missing", Arg.Any<CancellationToken>())
            .Returns((AddressDetail?)null);

        var result = await harness.Controller.Lookup(
            Token, "here:af:street:missing", TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Lookup_returns_the_address_detail()
    {
        var harness = new Harness();
        var detail = new AddressDetail { Street = "12 Queen Street", City = "Auckland", CountryCode = "NZ" };
        harness.Lookup.LookupAsync("here:af:street:abc", Arg.Any<CancellationToken>()).Returns(detail);

        var result = await harness.Controller.Lookup(
            Token, "here:af:street:abc", TestContext.Current.CancellationToken);

        Assert.Same(detail, Assert.IsType<OkObjectResult>(result.Result).Value);
    }
}
