using System.Net.Http.Json;
using System.Text.Json;
using BaggageDelivery.Core.Enums;
using BaggageDelivery.Core.Interfaces;
using BaggageDelivery.Core.Models;
using BaggageDelivery.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace BaggageDelivery.IntegrationTests;

public class PaxAddressGuardRailAcceptedTests : IAsyncLifetime
{
    private const string RequestTokenCookie = "XSRF-TOKEN";
    private const int JobId = 7002;
    private const int ClientId = 89;
    private const int EconomyRunSpeed = 37;

    private static IAvailableServicesQuery OfferingEconomyRun()
    {
        var query = Substitute.For<IAvailableServicesQuery>();
        query.ExecuteAsync(Arg.Any<AvailableServicesRequest>(), Arg.Any<CancellationToken>())
            .Returns([
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
        return query;
    }

    private readonly PaxApiFactory _factory = new()
    {
        AddressGuardRailsEnabled = true,
        AvailableServices = OfferingEconomyRun()
    };

    public async ValueTask InitializeAsync() => await SeedAsync();

    public ValueTask DisposeAsync()
    {
        _factory.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task A_serviceable_new_address_offers_the_allowed_service()
    {
        var response = await PostAsync("services", new { address = Address("Riccarton", "8041") });

        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.False(body.RootElement.GetProperty("noServiceAvailable").GetBoolean());
        var service = Assert.Single(body.RootElement.GetProperty("services").EnumerateArray());
        Assert.Equal(EconomyRunSpeed, service.GetProperty("jobTypeId").GetInt32());
        Assert.Equal("Economy Run", service.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Confirming_a_changed_address_with_an_offered_service_writes_the_speed()
    {
        var response = await PostAsync("confirm", new
        {
            address = Address("Riccarton", "8041"),
            deliveryTimeUtc = new DateTime(2026, 6, 10, 21, 0, 0, DateTimeKind.Utc),
            serviceJobTypeId = EconomyRunSpeed,
            atlOptionId = (int?)null,
            accessNotes = (string?)null,
            passengerName = "Jane Pax",
            passengerPhone = "+64211234567",
            passengerEmail = "jane@example.com"
        });

        response.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaggageDeliveryContext>();
        var job = await db.TucJobs.AsNoTracking()
            .SingleAsync(j => j.UcjbId == JobId, TestContext.Current.CancellationToken);

        Assert.Equal(EconomyRunSpeed, job.UcjbSpeed);
        Assert.Equal("Riccarton", job.DeliveryAddressLine5);
        Assert.Equal((int)JobStatus.New, job.UcjbStatus);
    }

    private async Task<HttpResponseMessage> PostAsync(string path, object body)
    {
        var client = _factory.CreateClient();

        var tokenResponse = await client.GetAsync("/api/v1/antiforgery/token",
            TestContext.Current.CancellationToken);
        tokenResponse.EnsureSuccessStatusCode();

        var requestToken = CookieValue(tokenResponse.Headers.GetValues("Set-Cookie")
            .Single(c => c.StartsWith($"{RequestTokenCookie}=", StringComparison.Ordinal)));

        var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/v1/pax/{EncryptedId(JobId)}/booking/{path}")
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("X-XSRF-TOKEN", requestToken);

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static object Address(string suburb, string postCode) => new
    {
        line3 = "1",
        line4 = "Test Street",
        line5 = suburb,
        line6 = "Auckland",
        line7 = postCode,
        country = "NZ"
    };

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
            UcclCode = "TAI2", Smsname = "TestAir", SiteId = 1,
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
