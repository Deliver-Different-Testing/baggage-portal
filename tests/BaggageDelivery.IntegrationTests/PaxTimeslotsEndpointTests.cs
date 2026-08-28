using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using BaggageDelivery.Core.Enums;
using BaggageDelivery.Core.Interfaces;
using BaggageDelivery.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BaggageDelivery.IntegrationTests;

public partial class PaxTimeslotsEndpointTests(PaxApiFactory factory) : IClassFixture<PaxApiFactory>
{
    private const int ClientId = 991;
    private const int BaggageSpeed = 38;

    [GeneratedRegex(@"^\d{1,2}:\d{2} (AM|PM) – \d{1,2}:\d{2} (AM|PM)$")]
    private static partial Regex WindowLabel { get; }

    [Fact]
    public async Task Timeslots_returns_eight_dated_windows_spanning_more_than_one_day()
    {
        const int jobId = 4301;
        await SeedRunClientJobAsync(jobId);

        var client = factory.CreateClient();

        var slots = await client.GetFromJsonAsync<TimeSlotResponse[]>(
            $"/api/v1/pax/{EncryptedId(jobId)}/booking/timeslots",
            TestContext.Current.CancellationToken);

        Assert.NotNull(slots);
        Assert.Equal(8, slots.Length);

        Assert.All(slots, s =>
        {
            Assert.False(string.IsNullOrWhiteSpace(s.DayLabel));
            Assert.Matches(WindowLabel, s.Label);
        });

        Assert.True(slots[0].FirstAvailable);
        Assert.Single(slots, s => s.FirstAvailable);
        Assert.True(slots.Select(s => s.DayLabel).Distinct().Count() > 1);
        Assert.Equal(slots.Select(s => s.RunUtc).Order(), slots.Select(s => s.RunUtc));
    }

    [Fact]
    public async Task Timeslots_is_empty_for_a_job_with_no_client()
    {
        const int jobId = 4302;
        await factory.SeedAsync(async db =>
        {
            db.TucJobs.Add(new TucJob
            {
                UcjbId = jobId, UcjbNumber = $"TEST-{jobId}", UcjbStatus = (int)JobStatus.Dispatched
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/v1/pax/{EncryptedId(jobId)}/booking/timeslots",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("[]", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private Task SeedRunClientJobAsync(int jobId) => factory.SeedAsync(async db =>
    {
        var ct = TestContext.Current.CancellationToken;

        if (!await db.TucClients.AnyAsync(c => c.UcclId == ClientId, ct))
        {
            db.TucClients.Add(new TucClient
            {
                UcclId = ClientId,
                UcclName = "Test Airline",
                UcclLegalName = "Test Airline Ltd",
                UcclCode = "TA",
                Smsname = "TA",
                CreatedBy = "test",
                LastModifiedBy = "test",
                SiteId = 1,
                EconomyRuns = true,
                EconomyRun1 = new DateTime(1900, 1, 1, 9, 0, 0),
                EconomyRun2 = new DateTime(1900, 1, 1, 12, 30, 0),
                EconomyRun3 = new DateTime(1900, 1, 1, 15, 0, 0),
                EconomyRun4 = new DateTime(1900, 1, 1, 17, 0, 0)
            });

            db.TucJobTypes.Add(new TucJobType
            {
                UcjtId = BaggageSpeed,
                UcjtName = "Baggage",
                CreatedBy = "test",
                LastModifiedBy = "test",
                Minutes = 180
            });
        }

        db.TucJobs.Add(new TucJob
        {
            UcjbId = jobId,
            UcjbNumber = $"TEST-{jobId}",
            UcjbStatus = (int)JobStatus.Dispatched,
            UcjbClientId = ClientId,
            UcjbSpeed = BaggageSpeed
        });

        await db.SaveChangesAsync(ct);
    });

    private string EncryptedId(int jobId)
    {
        using var scope = factory.Services.CreateScope();
        var encryption = scope.ServiceProvider.GetRequiredService<IEncryptionService>();
        return encryption.EncryptId(jobId);
    }

    private sealed record TimeSlotResponse(
        Guid Id, DateTime RunUtc, string DayLabel, string Label, bool FirstAvailable);
}
