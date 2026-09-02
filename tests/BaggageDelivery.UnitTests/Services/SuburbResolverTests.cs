using BaggageDelivery.Core.Services;
using BaggageDelivery.UnitTests.Helpers;
using Xunit;

namespace BaggageDelivery.UnitTests.Services;

public class SuburbResolverTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Resolve_returns_null_for_a_blank_suburb_name(string? suburbName)
    {
        await using var db = InMemoryDb.NewContext();

        var resolved = await new SuburbResolver(db)
            .ResolveAsync(suburbName, "1011", TestContext.Current.CancellationToken);

        Assert.Null(resolved);
    }

    [Fact]
    public async Task Resolve_returns_null_when_the_lookup_is_unavailable()
    {
        await using var db = InMemoryDb.NewContext();

        var resolved = await new SuburbResolver(db)
            .ResolveAsync("Ponsonby", "1011", TestContext.Current.CancellationToken);

        Assert.Null(resolved);
    }
}
