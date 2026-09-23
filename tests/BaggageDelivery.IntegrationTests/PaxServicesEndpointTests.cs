using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BaggageDelivery.Core.Enums;
using BaggageDelivery.Core.Services;
using BaggageDelivery.Core.Interfaces;
using BaggageDelivery.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace BaggageDelivery.IntegrationTests;

public class PaxServicesEndpointTests : IAsyncLifetime
{
    private const string RequestTokenCookie = "XSRF-TOKEN";
    private const int JobId = 7101;
    private const int ClientId = 91;
    private const int EconomyRunSpeed = 37;

    private readonly List<AvailableServicesRequest> _requests = [];
    private readonly PaxApiFactory _factory;

    public PaxServicesEndpointTests()
    {
        var query = Substitute.For<IAvailableServicesQuery>();
        query.ExecuteAsync(Arg.Any<AvailableServicesRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                _requests.Add(call.Arg<AvailableServicesRequest>());
                return Task.FromResult<IReadOnlyList<BagDel_stpAvailableServicesResult>>([
                    new BagDel_stpAvailableServicesResult
                    {
                        JobTypeID = EconomyRunSpeed,
                        Name = "Economy Run",
                        Speed = "ER",
                        Description = "Delivered on our next run",
                        Availability = "Available",
                        AvailabilityColour = "#00FF00",
                        Duration = 180
                    }
                ]);
            });

        _factory = new PaxApiFactory
        {
            AddressGuardRailsEnabled = true,
            AvailableServices = query
        };
    }

    public async ValueTask InitializeAsync() => await SeedAsync();

    public ValueTask DisposeAsync()
    {
        _factory.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task A_venue_without_a_street_number_is_still_checked_for_services()
    {
        var response = await PostServicesAsync(new { line5 = "Riccarton", line7 = "8041" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var service = Assert.Single(body.RootElement.GetProperty("services").EnumerateArray());
        Assert.Equal(EconomyRunSpeed, service.GetProperty("jobTypeId").GetInt32());

        var request = Assert.Single(_requests);
        Assert.Equal("Riccarton", request.ToSuburb);
        Assert.Equal(8041, request.ToPostCode);
    }

    [Fact]
    public async Task A_venue_with_only_coordinates_is_checked_by_position()
    {
        var response = await PostServicesAsync(new { latitude = -41.2865m, longitude = 174.7762m });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var request = Assert.Single(_requests);
        Assert.Equal(-41.2865m, request.ToLatitude);
        Assert.Equal(174.7762m, request.ToLongitude);
    }

    private async Task<HttpResponseMessage> PostServicesAsync(object address)
    {
        var client = _factory.CreateClient();

        var tokenResponse = await client.GetAsync("/api/v1/antiforgery/token",
            TestContext.Current.CancellationToken);
        tokenResponse.EnsureSuccessStatusCode();

        var requestToken = CookieValue(tokenResponse.Headers.GetValues("Set-Cookie")
            .Single(c => c.StartsWith($"{RequestTokenCookie}=", StringComparison.Ordinal)));

        var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/v1/pax/{EncryptedId(JobId)}/booking/services")
        {
            Content = JsonContent.Create(new { address })
        };
        request.Headers.Add("X-XSRF-TOKEN", requestToken);

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private string EncryptedId(int jobId)
    {
        using var scope = _factory.Services.CreateScope();
        var encryption = scope.ServiceProvider.GetRequiredService<IEncryptionService>();
        return encryption.EncryptId(jobId);
    }

    private static string CookieValue(string setCookie) =>
        setCookie.Split(';')[0].Split('=', 2)[1];

    private Task SeedAsync() => _factory.SeedAsync(async db =>
    {
        db.TucJobTypes.Add(new TucJobType
        {
            UcjtId = EconomyRunSpeed, UcjtName = "Economy Run", ShortName = "ER",
            UcjtDescription = "Economy Run", UcjtCode = "ER", JobLetter = "E",
            SystemName = "ER", Minutes = 180, CreatedBy = "test", LastModifiedBy = "test",
            Notes = string.Empty, ExtraName = string.Empty, Alias = string.Empty
        });
        db.TucClients.Add(new TucClient
        {
            UcclId = ClientId, UcclName = "Test Air", UcclLegalName = "Test Air Ltd",
            UcclCode = "TAI3", Smsname = "TestAir", SiteId = 1,
            EconomyActive = true, EconomyRuns = true,
            CreatedBy = "test", LastModifiedBy = "test"
        });
        db.TucJobs.Add(new TucJob
        {
            UcjbId = JobId,
            UcjbNumber = $"TEST-{JobId}",
            UcjbClientId = ClientId,
            UcjbSize = 2,
            UcjbSpeed = EconomyRunSpeed,
            DeliveryAddressLine3 = "1",
            DeliveryAddressLine4 = "Test Street",
            DeliveryAddressLine5 = "Ponsonby",
            DeliveryAddressLine6 = "Auckland",
            DeliveryAddressLine7 = "1011",
            DeliveryAddressLine8 = "NZ",
            UcjbToAddr = "1, Test Street, Ponsonby, Auckland, 1011, NZ",
            UcjbStatus = (int)JobStatus.Dispatched
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    });
}
