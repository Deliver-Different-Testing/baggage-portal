using BaggageDelivery.Core.Http.Models;
using BaggageDelivery.Core.Services;
using Xunit;

namespace BaggageDelivery.UnitTests.Services;

public class DespatchAddressComposerTests
{
    private static AddressUpdateDto Address(
        string? line1 = null, string? line2 = null, string? line3 = null,
        string line4 = "Test Street", string line5 = "Ponsonby", string line6 = "Auckland",
        string? line7 = null) =>
        new()
        {
            Line1 = line1, Line2 = line2, Line3 = line3, Line4 = line4,
            Line5 = line5, Line6 = line6, Line7 = line7, Country = "NZ"
        };

    [Fact]
    public void Compose_joins_every_line_in_order_and_appends_the_country()
    {
        var result = DespatchAddressComposer.Compose(
            Address("Acme Co", "Unit 5", "7", "Low Lane", "Wellington", "WGN", "6011"), "NZ");

        Assert.Equal("Acme Co, Unit 5, 7, Low Lane, Wellington, WGN, 6011, NZ", result);
    }

    [Fact]
    public void Compose_skips_blank_and_whitespace_only_lines()
    {
        var result = DespatchAddressComposer.Compose(
            Address(line1: null, line2: "   ", line3: "7"), "NZ");

        Assert.Equal("7, Test Street, Ponsonby, Auckland, NZ", result);
    }

    [Fact]
    public void Compose_trims_surrounding_whitespace_on_each_line()
    {
        var result = DespatchAddressComposer.Compose(
            Address(line1: "  Acme Co  ", line4: " Low Lane "), "NZ");

        Assert.Equal("Acme Co, Low Lane, Ponsonby, Auckland, NZ", result);
    }

    [Fact]
    public void Compose_truncates_to_the_column_width()
    {
        var result = DespatchAddressComposer.Compose(
            Address(line1: new string('a', 145), line2: "Unit 5"), "NZ");

        Assert.Equal(150, result.Length);
        Assert.EndsWith("a, Uni", result);
    }

    [Fact]
    public void Compose_drops_a_dangling_separator_left_by_truncation()
    {
        var result = DespatchAddressComposer.Compose(
            Address(line1: new string('a', 149), line2: "Unit 5"), "NZ");

        Assert.Equal(new string('a', 149), result);
    }
}
