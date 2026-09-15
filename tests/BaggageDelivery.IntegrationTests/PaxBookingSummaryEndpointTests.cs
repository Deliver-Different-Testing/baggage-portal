using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BaggageDelivery.Core.Enums;
using BaggageDelivery.Core.Interfaces;
using BaggageDelivery.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BaggageDelivery.IntegrationTests;

public class PaxBookingSummaryEndpointTests(PaxApiFactory factory) : IClassFixture<PaxApiFactory>
{
    [Fact]
    public async Task Booking_serves_the_worldtracer_file_ref_as_the_file_reference()
    {
        const int jobId = 4401;
        await SeedJobAsync(jobId, "URG-4401", clientRefa: "AKLNZ12345");

        var client = factory.CreateClient();

        var payload = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/pax/{EncryptedId(jobId)}/booking",
            TestContext.Current.CancellationToken);

        Assert.Equal("AKLNZ12345", payload.GetProperty("fileReference").GetString());
        Assert.Equal("URG-4401", payload.GetProperty("jobNumber").GetString());
        Assert.Equal("NZ", payload.GetProperty("airlineCode").GetString());
    }

    [Fact]
    public async Task Booking_serves_an_empty_file_reference_when_the_job_has_none()
    {
        const int jobId = 4402;
        await SeedJobAsync(jobId, jobNumber: null, clientRefa: null);

        var client = factory.CreateClient();

        var payload = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/pax/{EncryptedId(jobId)}/booking",
            TestContext.Current.CancellationToken);

        Assert.Equal(string.Empty, payload.GetProperty("fileReference").GetString());
    }

    [Fact]
    public async Task Booking_serves_the_sms_name_as_the_airline_label_so_it_matches_notifications()
    {
        const int jobId = 4405;
        const int clientId = 4405;
        await factory.SeedAsync(async db =>
        {
            db.TucClients.Add(new TucClient
            {
                UcclId = clientId, UcclName = "Air New Zealand", UcclLegalName = "Air New Zealand Ltd",
                UcclCode = "ANZ4405", Smsname = "Air NZ", SiteId = 1,
                CreatedBy = "test", LastModifiedBy = "test"
            });
            db.TucJobs.Add(new TucJob
            {
                UcjbId = jobId,
                UcjbNumber = "URG-4405",
                UcjbClientId = clientId,
                UcjbStatus = (int)JobStatus.Dispatched
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        var client = factory.CreateClient();

        var payload = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/pax/{EncryptedId(jobId)}/booking",
            TestContext.Current.CancellationToken);

        Assert.Equal("Air NZ", payload.GetProperty("airlineLabel").GetString());
    }

    [Fact]
    public async Task Booking_falls_back_to_the_client_name_as_the_airline_label_when_no_sms_name_is_set()
    {
        const int jobId = 4406;
        const int clientId = 4406;
        await factory.SeedAsync(async db =>
        {
            db.TucClients.Add(new TucClient
            {
                UcclId = clientId, UcclName = "Qantas", UcclLegalName = "Qantas Airways Ltd",
                UcclCode = "ANZ4406", Smsname = "", SiteId = 1,
                CreatedBy = "test", LastModifiedBy = "test"
            });
            db.TucJobs.Add(new TucJob
            {
                UcjbId = jobId,
                UcjbNumber = "URG-4406",
                UcjbClientId = clientId,
                UcjbStatus = (int)JobStatus.Dispatched
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        var client = factory.CreateClient();

        var payload = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/pax/{EncryptedId(jobId)}/booking",
            TestContext.Current.CancellationToken);

        Assert.Equal("Qantas", payload.GetProperty("airlineLabel").GetString());
    }

    [Fact]
    public async Task The_in_app_tracking_endpoint_is_gone()
    {
        const int jobId = 4404;
        await SeedJobAsync(jobId, "URG-4404", clientRefa: null);

        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/pax/{EncryptedId(jobId)}/tracking",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private Task SeedJobAsync(int jobId, string? jobNumber, string? clientRefa) =>
        factory.SeedAsync(async db =>
        {
            db.TucJobs.Add(new TucJob
            {
                UcjbId = jobId,
                UcjbNumber = jobNumber,
                UcjbClientRefa = clientRefa,
                UcjbStatus = (int)JobStatus.Dispatched
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

    private string EncryptedId(int jobId)
    {
        using var scope = factory.Services.CreateScope();
        var encryption = scope.ServiceProvider.GetRequiredService<IEncryptionService>();
        return encryption.EncryptId(jobId);
    }
}
