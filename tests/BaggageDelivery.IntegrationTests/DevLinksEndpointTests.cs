using System.Net;
using System.Net.Http.Json;
using BaggageDelivery.Api.Dev;
using BaggageDelivery.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BaggageDelivery.IntegrationTests;

public class DevLinksEndpointTests(PaxApiFactory factory) : IClassFixture<PaxApiFactory>
{
    private sealed record DevLinksResponse(int JobId, string Token, string ConfirmUrl);

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
    public async Task Dev_links_url_targets_the_confirm_route_on_the_spa_origin()
    {
        var client = factory.CreateClient();

        var links = await client.GetFromJsonAsync<DevLinksResponse>("/api/v1/dev/links",
            TestContext.Current.CancellationToken);

        Assert.NotNull(links);

        var paxBase = DevStartup.ResolvePaxBaseUrl(Environment.GetEnvironmentVariable("AppUrl"));

        Assert.Equal($"{paxBase}/c/{links.Token}", links.ConfirmUrl);
    }
}
