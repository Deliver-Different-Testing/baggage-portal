using BaggageDelivery.Core.Globalization;
using Xunit;

namespace BaggageDelivery.UnitTests.Globalization;

public class CountryCodesTests
{
    [Theory]
    [InlineData("NZ", "NZ")]
    [InlineData("nz", "NZ")]
    [InlineData("New Zealand", "NZ")]
    [InlineData("NEW ZEALAND", "NZ")]
    [InlineData("  new zealand  ", "NZ")]
    [InlineData("NZL", "NZ")]
    [InlineData("N.Z.", "NZ")]
    [InlineData("Australia", "AU")]
    [InlineData("AUS", "AU")]
    [InlineData("USA", "US")]
    [InlineData("United States", "US")]
    [InlineData("United States of America", "US")]
    [InlineData("U.S.A.", "US")]
    [InlineData("UK", "GB")]
    [InlineData("Great Britain", "GB")]
    [InlineData("GBR", "GB")]
    [InlineData("Fiji", "FJ")]
    [InlineData("FJI", "FJ")]
    [InlineData("Singapore", "SG")]
    public void TryToIso2_normalises_legacy_free_text(string input, string expected)
    {
        Assert.True(CountryCodes.TryToIso2(input, out var iso2));
        Assert.Equal(expected, iso2);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Wakanda")]
    [InlineData("XX")]
    [InlineData("QQ")]
    public void TryToIso2_returns_false_for_unrecognised(string? input)
    {
        Assert.False(CountryCodes.TryToIso2(input, out var iso2));
        Assert.Null(iso2);
    }

    [Theory]
    [InlineData("NZ", "NZL")]
    [InlineData("AU", "AUS")]
    [InlineData("US", "USA")]
    [InlineData("GB", "GBR")]
    [InlineData("FJ", "FJI")]
    [InlineData("SG", "SGP")]
    public void ToIso3_maps_beyond_the_legacy_ten(string iso2, string expected) =>
        Assert.Equal(expected, CountryCodes.ToIso3(iso2));

    [Fact]
    public void ToIso3_returns_null_for_unrecognised()
    {
        Assert.Null(CountryCodes.ToIso3("Wakanda"));
        Assert.Null(CountryCodes.ToIso3(null));
    }

    [Theory]
    [InlineData("US")]
    [InlineData("us")]
    [InlineData("USA")]
    [InlineData("United States")]
    [InlineData("United States of America")]
    [InlineData("U.S.A.")]
    public void IsUnitedStates_matches_all_spellings(string input) => Assert.True(CountryCodes.IsUnitedStates(input));

    [Theory]
    [InlineData("NZ")]
    [InlineData("New Zealand")]
    [InlineData("AU")]
    [InlineData(null)]
    [InlineData("")]
    public void IsUnitedStates_is_false_for_everything_else(string? input) =>
        Assert.False(CountryCodes.IsUnitedStates(input));

    [Fact]
    public void Country_table_is_populated_from_region_info() =>
        Assert.True(CountryCodes.KnownCountryCount >= 200,
            $"Only {CountryCodes.KnownCountryCount} countries resolved — globalization data is missing.");
}
