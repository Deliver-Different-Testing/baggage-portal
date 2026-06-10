using BaggageDelivery.Core.Enums;
using BaggageDelivery.Core.Http;
using BaggageDelivery.Core.Http.Models;
using BaggageDelivery.Core.Models;
using BaggageDelivery.Core.Services;
using BaggageDelivery.UnitTests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace BaggageDelivery.UnitTests.Services;

public class PaxBookingServiceTests
{
    private static IOptions<DespatchOptions> DespatchOpts() =>
        Options.Create(new DespatchOptions { TimeZone = "Pacific/Auckland" });

    [Fact]
    public async Task GetTimeslots_builds_consecutive_run_pairs_from_first_eco_setting()
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc));

        db.TblEcoSettings.Add(new TblEcoSetting
        {
            SettingId = 1,
            EconomyRun1 = new DateTime(1900, 1, 1, 9, 0, 0),
            EconomyRun2 = new DateTime(1900, 1, 1, 12, 0, 0),
            EconomyRun3 = new DateTime(1900, 1, 1, 15, 0, 0),
            EconomyRun4 = new DateTime(1900, 1, 1, 17, 0, 0),
            EconomyRun5 = new DateTime(1900, 1, 1, 19, 0, 0)
        });
        await db.SaveChangesAsync(CancellationToken.None);

        var svc = new PaxBookingService(db, DespatchOpts(), time);

        var slots = await svc.GetTimeslotsAsync(
            jobId: 42,
            localDate: new DateTime(2026, 6, 10),
            CancellationToken.None);

        Assert.Equal(4, slots.Count);
        Assert.Equal("9:00 AM - 12:00 PM", slots[0].Label);
        Assert.Equal("12:00 PM - 3:00 PM", slots[1].Label);
        Assert.Equal("3:00 PM - 5:00 PM", slots[2].Label);
        Assert.Equal("5:00 PM - 7:00 PM", slots[3].Label);
    }

    [Fact]
    public async Task GetTimeslots_picks_first_record_when_multiple_settings_exist()
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc));

        db.TblEcoSettings.Add(new TblEcoSetting
        {
            SettingId = 1,
            EconomyRun1 = new DateTime(1900, 1, 1, 8, 0, 0),
            EconomyRun2 = new DateTime(1900, 1, 1, 11, 0, 0)
        });
        db.TblEcoSettings.Add(new TblEcoSetting
        {
            SettingId = 2,
            EconomyRun1 = new DateTime(1900, 1, 1, 14, 0, 0),
            EconomyRun2 = new DateTime(1900, 1, 1, 18, 0, 0)
        });
        await db.SaveChangesAsync(CancellationToken.None);

        var svc = new PaxBookingService(db, DespatchOpts(), time);

        var slots = await svc.GetTimeslotsAsync(
            jobId: 42,
            localDate: new DateTime(2026, 6, 10),
            CancellationToken.None);

        Assert.Single(slots);
        Assert.Equal("8:00 AM - 11:00 AM", slots[0].Label);
    }

    [Fact]
    public async Task GetTimeslots_skips_null_run_columns()
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc));

        db.TblEcoSettings.Add(new TblEcoSetting
        {
            SettingId = 1,
            EconomyRun1 = new DateTime(1900, 1, 1, 9, 0, 0),
            EconomyRun2 = new DateTime(1900, 1, 1, 12, 0, 0),
            EconomyRun3 = null,
            EconomyRun4 = new DateTime(1900, 1, 1, 17, 0, 0),
            EconomyRun5 = new DateTime(1900, 1, 1, 19, 0, 0)
        });
        await db.SaveChangesAsync(CancellationToken.None);

        var svc = new PaxBookingService(db, DespatchOpts(), time);

        var slots = await svc.GetTimeslotsAsync(
            jobId: 42,
            localDate: new DateTime(2026, 6, 10),
            CancellationToken.None);

        Assert.Equal(2, slots.Count);
        Assert.Equal("9:00 AM - 12:00 PM", slots[0].Label);
        Assert.Equal("5:00 PM - 7:00 PM", slots[1].Label);
    }

    [Fact]
    public async Task GetTimeslots_returns_empty_when_no_eco_setting_rows()
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc));

        var svc = new PaxBookingService(db, DespatchOpts(), time);

        var slots = await svc.GetTimeslotsAsync(
            jobId: 42,
            localDate: new DateTime(2026, 6, 10),
            CancellationToken.None);

        Assert.Empty(slots);
    }

    [Fact]
    public async Task Confirm_updates_tucJob_and_writes_baggage_delivery_booking_journey_row()
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 3, 0, 0, DateTimeKind.Utc));

        var ct = TestContext.Current.CancellationToken;
        db.TucJobs.Add(new TucJob
        {
            UcjbId = 4242,
            UcjbNumber = "TEST-4242",
            DeliverToContact = "old name",
            UcjbStatus = (int)JobStatus.Dispatched
        });
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), time);

        await svc.ConfirmAsync(new ConfirmBookingInput(
            JobId: 4242,
            Address: new AddressUpdateDto
            {
                Line1 = "1 Test Street",
                City = "Auckland",
                Country = "NZ"
            },
            TimeSlotStartUtc: new DateTime(2026, 6, 10, 21, 0, 0, DateTimeKind.Utc),
            TimeSlotEndUtc: new DateTime(2026, 6, 11, 0, 0, 0, DateTimeKind.Utc),
            AtlOption: null!,
            AccessNotes: "Buzzer 3",
            PassengerName: "Jane Pax",
            PassengerPhone: "+64 21 000",
            PassengerEmail: "jane@example.com"), ct);

        var job = await db.TucJobs.AsNoTracking().SingleAsync(j => j.UcjbId == 4242, ct);
        Assert.Equal("Jane Pax", job.DeliverToContact);
        Assert.Equal((int)JobStatus.New, job.UcjbStatus);

        var journey = await db.JobDeliveryJourneys.AsNoTracking().SingleAsync(j => j.JobId == 4242, ct);
        Assert.Equal(nameof(DeliveryJourneyChangeType.BaggageDeliveryBooking), journey.ChangeType);
        Assert.Equal(nameof(DeliveryJourneyUpdatedByType.System), journey.UpdatedByType);
        Assert.Equal(time.GetUtcNow().UtcDateTime, journey.UpdatedAt);
        Assert.Contains("self-service", journey.Comments);
    }

    [Fact]
    public async Task Confirm_throws_when_tucJob_does_not_exist()
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 3, 0, 0, DateTimeKind.Utc));
        var svc = new PaxBookingService(db, DespatchOpts(), time);
        var ct = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ConfirmAsync(new ConfirmBookingInput(
            JobId: 9999,
            Address: new AddressUpdateDto { Line1 = "x", City = "y", Country = "NZ" },
            TimeSlotStartUtc: new DateTime(2026, 6, 10, 21, 0, 0, DateTimeKind.Utc),
            TimeSlotEndUtc: new DateTime(2026, 6, 11, 0, 0, 0, DateTimeKind.Utc),
            AtlOption: null!,
            AccessNotes: null,
            PassengerName: "X",
            PassengerPhone: null,
            PassengerEmail: null), ct));

        Assert.False(await db.JobDeliveryJourneys.AnyAsync(ct));
    }

    [Fact]
    public async Task GetTimeslots_marks_first_slot_whose_end_is_future_as_first_available()
    {
        await using var db = InMemoryDb.NewContext();
        // 2026-06-10 03:00 UTC = 2026-06-10 15:00 Pacific/Auckland (NZST UTC+12).
        // Slots are 9-12 (past), 12-15 (ending at 'now', not future), 15-17 (future).
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 3, 0, 0, DateTimeKind.Utc));

        db.TblEcoSettings.Add(new TblEcoSetting
        {
            SettingId = 1,
            EconomyRun1 = new DateTime(1900, 1, 1, 9, 0, 0),
            EconomyRun2 = new DateTime(1900, 1, 1, 12, 0, 0),
            EconomyRun3 = new DateTime(1900, 1, 1, 15, 0, 0),
            EconomyRun4 = new DateTime(1900, 1, 1, 17, 0, 0)
        });
        await db.SaveChangesAsync(CancellationToken.None);

        var svc = new PaxBookingService(db, DespatchOpts(), time);

        var slots = await svc.GetTimeslotsAsync(
            jobId: 42,
            localDate: new DateTime(2026, 6, 10),
            CancellationToken.None);

        Assert.False(slots[0].FirstAvailable);
        Assert.False(slots[1].FirstAvailable);
        Assert.True(slots[2].FirstAvailable);
    }
}
