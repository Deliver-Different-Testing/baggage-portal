using System.Net;
using System.Net.Http.Json;
using BaggageDelivery.Api.Dev;
using BaggageDelivery.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BaggageDelivery.IntegrationTests;

// PaxApiFactory forces ASPNETCORE_ENVIRONMENT=Development, so MapDevLinks registers
// the route here. Outside Development it is never mapped — that 404 is what the SPA's
// / route relies on to fall back to /expired.
public class DevLinksEndpointTests(PaxApiFactory factory) : IClassFixture<PaxApiFactory>
{
    private sealed record DevLinksResponse(int JobId, string Token, string ConfirmUrl, string TrackUrl);

    [Fact]
    public async Task Dev_links_returns_a_token_that_decrypts_back_to_the_configured_job()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/dev/links", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var links = await response.Content.ReadFromJsonAsync<DevLinksResponse>(
            TestContext.Current.CancellationToken);

        Assert.NotNull(links);
        Assert.False(string.IsNullOrWhiteSpace(links.Token));

        using var scope = factory.Services.CreateScope();
        var encryptor = scope.ServiceProvider.GetRequiredService<IEncryptionService>();
        Assert.Equal(links.JobId, encryptor.DecryptId(links.Token));
    }

    [Fact]
    public async Task Dev_links_urls_target_the_pax_routes_on_the_spa_origin()
    {
        var client = factory.CreateClient();

        var links = await client.GetFromJsonAsync<DevLinksResponse>("/api/v1/dev/links",
            TestContext.Current.CancellationToken);

        Assert.NotNull(links);

        // The pax pages are served by the Vite dev server, not by Kestrel, so the links
        // must resolve against AppUrl (defaulting to :5173) rather than the API's origin.
        var paxBase = DevStartup.ResolvePaxBaseUrl(Environment.GetEnvironmentVariable("AppUrl"));

        Assert.Equal($"{paxBase}/c/{links.Token}", links.ConfirmUrl);
        Assert.Equal($"{paxBase}/t/{links.Token}", links.TrackUrl);
    }
}
