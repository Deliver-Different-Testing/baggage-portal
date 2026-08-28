using System.Net.Http.Json;
using System.Text.Json;
using BaggageDelivery.Core.Enums;
using BaggageDelivery.Core.Interfaces;
using BaggageDelivery.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BaggageDelivery.IntegrationTests;

// The passenger's File Reference has to survive the whole way to the wire: the SPA
// reads `fileReference` off this payload and hides the tag when it's missing, so a
// renamed or dropped field is a silently blank reference on the confirm page.
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
        // The Urgent job number still reaches the passenger, but only through the
        // notification email — it is not a field the confirm page reads.
        Assert.False(payload.TryGetProperty("jobNumber", out _));
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
