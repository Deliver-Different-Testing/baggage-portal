using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BaggageDelivery.Core.Enums;
using BaggageDelivery.Core.Interfaces;
using BaggageDelivery.Core.Models;
using BaggageDelivery.Core.Services;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BaggageDelivery.IntegrationTests;

public class PaxAddressGuardRailTests : IAsyncLifetime
{
    private const string RequestTokenCookie = "XSRF-TOKEN";
    private const int JobId = 7001;
    private const int ClientId = 88;
    private const int EconomyRunSpeed = 37;

    private static IAvailableServicesQuery Offering(params BagDel_stpAvailableServicesResult[] rows)
    {
        var query = Substitute.For<IAvailableServicesQuery>();
        query.ExecuteAsync(Arg.Any<AvailableServicesRequest>(), Arg.Any<CancellationToken>())
            .Returns(rows);
        return query;
    }

    private readonly PaxApiFactory _factory = new()
    {
        AddressGuardRailsEnabled = true,
        AvailableServices = Offering()
    };

    public async ValueTask InitializeAsync() => await SeedAsync();

    public ValueTask DisposeAsync()
    {
        _factory.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task An_unserviceable_new_address_offers_no_services()
    {
        var response = await PostAsync("services", new { address = Address("Haast", "7886") });

        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.True(body.RootElement.GetProperty("noServiceAvailable").GetBoolean());
        Assert.Empty(body.RootElement.GetProperty("services").EnumerateArray());
    }

    [Fact]
    public async Task Confirming_a_changed_address_without_a_service_is_a_validation_problem()
    {
        var response = await PostAsync("confirm", ConfirmBody("Haast", "7886", serviceJobTypeId: null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("Service", out _));

        await AssertJobUntouchedAsync();
    }

    [Fact]
    public async Task Confirming_a_changed_address_with_an_unavailable_service_is_refused()
    {
        var response = await PostAsync("confirm",
            ConfirmBody("Haast", "7886", serviceJobTypeId: EconomyRunSpeed));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertJobUntouchedAsync();
    }

    [Fact]
    public async Task Asking_the_airline_to_get_in_touch_queues_a_message_and_leaves_the_job_alone()
    {
        var response = await PostAsync("address-help", new
        {
            address = Address("Haast", "7886"),
            passengerName = "Jane Pax",
            passengerPhone = "+64211234567",
            passengerEmail = "jane@example.com"
        });

        response.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaggageDeliveryContext>();
        var message = await db.TucManualMessages.AsNoTracking()
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(JobId, message.JobId);
        Assert.Equal("ops@airline.test", message.SendToEmailAddress);
        Assert.Contains("Haast", message.UcmmMessage);
        Assert.Contains("Jane Pax", message.UcmmMessage);

        await AssertJobUntouchedAsync();
    }

    [Fact]
    public async Task An_untouched_address_still_confirms_without_choosing_a_service()
    {
        var response = await PostAsync("confirm",
            ConfirmBody("Ponsonby", "1011", serviceJobTypeId: null));

        response.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaggageDeliveryContext>();
        var job = await db.TucJobs.AsNoTracking()
            .SingleAsync(j => j.UcjbId == JobId, TestContext.Current.CancellationToken);

        Assert.Equal((int)JobStatus.New, job.UcjbStatus);
    }

    private async Task AssertJobUntouchedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaggageDeliveryContext>();
        var job = await db.TucJobs.AsNoTracking()
            .SingleAsync(j => j.UcjbId == JobId, TestContext.Current.CancellationToken);

        Assert.Equal((int)JobStatus.Dispatched, job.UcjbStatus);
        Assert.Equal("Ponsonby", job.DeliveryAddressLine5);
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

    private static object ConfirmBody(string suburb, string postCode, int? serviceJobTypeId) => new
    {
        address = Address(suburb, postCode),
        deliveryTimeUtc = new DateTime(2026, 6, 10, 21, 0, 0, DateTimeKind.Utc),
        serviceJobTypeId,
        atlOptionId = (int?)null,
        accessNotes = (string?)null,
        passengerName = "Jane Pax",
        passengerPhone = "+64211234567",
        passengerEmail = "jane@example.com"
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
            UcclCode = "TAIR", Smsname = "TestAir", SiteId = 1,
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
