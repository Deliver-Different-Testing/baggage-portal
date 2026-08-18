using System.Net.Http.Json;
using System.Text.Json;
using BaggageDelivery.Core.Enums;
using BaggageDelivery.Core.Interfaces;
using BaggageDelivery.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BaggageDelivery.IntegrationTests;

// The passenger's Booking Reference has to survive the whole way to the wire: the
// SPA reads `jobNumber` off this payload and hides the tag when it's missing, so a
// renamed or dropped field is a silently blank reference on the confirm page.
public class PaxBookingSummaryEndpointTests(PaxApiFactory factory) : IClassFixture<PaxApiFactory>
{
    [Fact]
    public async Task Booking_serves_the_urgent_job_number_as_the_booking_reference()
    {
        const int jobId = 4401;
        await SeedJobAsync(jobId, "URG-4401", clientRefa: "AKLNZ12345");

        var client = factory.CreateClient();

        var payload = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/pax/{EncryptedId(jobId)}/booking",
            TestContext.Current.CancellationToken);

        Assert.Equal("URG-4401", payload.GetProperty("jobNumber").GetString());
        // The WorldTracer file ref stays behind the airline-code derivation; it is
        // no longer a field the passenger flow reads.
        Assert.False(payload.TryGetProperty("reference", out _));
        Assert.Equal("NZ", payload.GetProperty("airlineCode").GetString());
    }

    [Fact]
    public async Task Booking_serves_an_empty_job_number_when_the_job_has_none()
    {
        const int jobId = 4402;
        await SeedJobAsync(jobId, jobNumber: null, clientRefa: null);

        var client = factory.CreateClient();

        var payload = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/pax/{EncryptedId(jobId)}/booking",
            TestContext.Current.CancellationToken);

        Assert.Equal(string.Empty, payload.GetProperty("jobNumber").GetString());
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
