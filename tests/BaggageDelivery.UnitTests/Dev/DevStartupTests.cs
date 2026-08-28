using BaggageDelivery.Api.Dev;
using BaggageDelivery.Core.Interfaces;
using NSubstitute;
using Xunit;

namespace BaggageDelivery.UnitTests.Dev;

public class DevStartupTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolvePaxBaseUrl_falls_back_to_the_vite_dev_server(string? appUrl) => Assert.Equal(DevStartup.DefaultPaxBaseUrl, DevStartup.ResolvePaxBaseUrl(appUrl));

    [Theory]
    [InlineData("https://baggage.example.com", "https://baggage.example.com")]
    [InlineData("https://baggage.example.com/", "https://baggage.example.com")]
    [InlineData("https://baggage.example.com///", "https://baggage.example.com")]
    public void ResolvePaxBaseUrl_honours_AppUrl_without_doubling_the_separator(
        string appUrl, string expected) =>
        Assert.Equal(expected, DevStartup.ResolvePaxBaseUrl(appUrl));

    [Fact]
    public void BuildLinks_points_at_the_pax_routes_for_the_minted_token()
    {
        var encryptor = Substitute.For<IEncryptionService>();
        encryptor.EncryptId(67).Returns("tok-67");

        var (links, error, _) = DevStartup.BuildLinks(encryptor, 67, "https://baggage.example.com/");

        Assert.Null(error);
        Assert.NotNull(links);

        Assert.Equal(67, links.JobId);
        Assert.Equal("tok-67", links.Token);
        Assert.Equal("https://baggage.example.com/c/tok-67", links.ConfirmUrl);
        Assert.Equal("https://baggage.example.com/t/tok-67", links.TrackUrl);
    }

    [Fact]
    public void BuildLinks_reports_a_missing_encryption_service_instead_of_throwing()
    {
        var result = DevStartup.BuildLinks(null, 67, null);

        Assert.Null(result.Links);
        Assert.Contains("IEncryptionService", result.Error);
    }

    [Fact]
    public void BuildLinks_reports_an_encryption_failure_instead_of_throwing()
    {
        var encryptor = Substitute.For<IEncryptionService>();
        encryptor.EncryptId(Arg.Any<int>()).Returns(_ => throw new InvalidOperationException("no key"));

        var result = DevStartup.BuildLinks(encryptor, 67, null);

        Assert.Null(result.Links);
        Assert.Contains("BaggageDeliveryEncryptionKey", result.Error);
        Assert.IsType<InvalidOperationException>(result.Exception);
    }
}
