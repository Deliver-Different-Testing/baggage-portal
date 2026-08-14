using BaggageDelivery.Core.Enums;
using BaggageDelivery.Core.Http;
using BaggageDelivery.Core.Interfaces;
using BaggageDelivery.Core.Http.Models;
using BaggageDelivery.Core.Models;
using BaggageDelivery.Core.Services;
using BaggageDelivery.UnitTests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace BaggageDelivery.UnitTests.Services;

public class PaxBookingServiceTests
{
    private static IOptions<DespatchOptions> DespatchOpts(string supportPhone = "") =>
        Options.Create(new DespatchOptions
        {
            TimeZone = "Pacific/Auckland",
            SupportPhone = supportPhone
        });

    // Fresh cache per service so reference-data caching can't leak between tests.
    private static MemoryCache NewCache() => new(new MemoryCacheOptions());

    // The real calendar calls UTL_IsBusinessDay / UTL_AddBusinessDays, which
    // don't exist on SQLite. Weekends are non-business unless stated otherwise.
    private sealed class FakeCalendar(params DateTime[] extraNonBusinessDays) : IDespatchCalendar
    {
        private bool IsBusiness(DateTime d) =>
            d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)
            && !extraNonBusinessDays.Contains(d.Date);

        public Task<bool> IsBusinessDayAsync(DateTime localDate, int clientId, CancellationToken ct) =>
            Task.FromResult(IsBusiness(localDate.Date));

        public Task<DateTime> AddBusinessDaysAsync(int days, DateTime localDate, int clientId,
            CancellationToken ct)
        {
            var d = localDate.Date;
            for (var i = 0; i < days; i++)
            {
                do
                {
                    d = d.AddDays(1);
                } while (!IsBusiness(d));
            }

            return Task.FromResult(d);
        }
    }

    private static FakeCalendar NewCalendar() => new();

    private static TucClient NewClient(int clientId, bool economyRuns, params TimeSpan[] runs)
    {
        return new TucClient
        {
            UcclId = clientId,
            UcclName = $"Client {clientId}",
            UcclLegalName = $"Client {clientId} Ltd",
            UcclCode = $"C{clientId}",
            Smsname = $"C{clientId}",
            CreatedBy = "test",
            LastModifiedBy = "test",
            SiteId = 1,
            EconomyRuns = economyRuns,
            EconomyRun1 = At(0),
            EconomyRun2 = At(1),
            EconomyRun3 = At(2),
            EconomyRun4 = At(3),
            EconomyRun5 = At(4),
            EconomyRun6 = At(5),
            EconomyRun7 = At(6),
            EconomyRun8 = At(7)
        };

        DateTime? At(int i) => i < runs.Length ? new DateTime(1900, 1, 1).Add(runs[i]) : null;
    }

    private static TucJob NewJob(int jobId, int clientId, int? speed = BaggageSpeed) =>
        new() { UcjbId = jobId, UcjbNumber = $"JOB-{jobId}", UcjbClientId = clientId, UcjbSpeed = speed };

    // tucJob.ucjbSpeed points at tucJobType.ucjtID; Minutes is how long the
    // promised window runs for.
    private const int BaggageSpeed = 38;

    private static TucJobType NewJobType(int speedId, int? minutes) => new()
    {
        UcjtId = speedId,
        UcjtName = $"Speed {speedId}",
        CreatedBy = "test",
        LastModifiedBy = "test",
        Minutes = minutes
    };

    private static readonly TimeSpan[] StandardRuns =
    [
        new(9, 0, 0), new(12, 30, 0), new(15, 0, 0), new(17, 0, 0)
    ];

    [Fact]
    public async Task GetTimeslots_returns_eight_windows_rolling_into_the_next_business_day()
    {
        await using var db = InMemoryDb.NewContext();
        var ct = TestContext.Current.CancellationToken;
        // 2026-06-09 12:00 UTC = 2026-06-10 00:00 Pacific/Auckland (NZST UTC+12),
        // so every run on the 10th is still ahead of "now".
        var time = new FakeTimeProvider(new DateTime(2026, 6, 9, 12, 0, 0, DateTimeKind.Utc));

        db.TucClients.Add(NewClient(77, economyRuns: true, StandardRuns));
        db.TucJobTypes.Add(NewJobType(BaggageSpeed, minutes: 180));
        db.TucJobs.Add(NewJob(4242, 77));
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        var slots = await svc.GetTimeslotsAsync(4242, localDate: new DateTime(2026, 6, 10), ct);

        // Four runs a day, so the eighth window is on Thursday the 11th.
        Assert.Equal(8, slots.Count);
        Assert.Equal("Today, Wed 10 Jun", slots[0].DayLabel);
        Assert.Equal("9:00 AM – 12:00 PM", slots[0].Label);
        Assert.Equal("12:30 PM – 3:30 PM", slots[1].Label);
        Assert.Equal("5:00 PM – 8:00 PM", slots[3].Label);
        Assert.Equal("Tomorrow, Thu 11 Jun", slots[4].DayLabel);
        Assert.Equal("9:00 AM – 12:00 PM", slots[4].Label);
        Assert.True(slots[0].FirstAvailable);
        Assert.DoesNotContain(slots.Skip(1), s => s.FirstAvailable);
        Assert.Equal(new DateTime(2026, 6, 9, 21, 0, 0, DateTimeKind.Utc), slots[0].RunUtc);
    }

    [Fact]
    public async Task GetTimeslots_ends_each_window_at_the_job_speed_duration()
    {
        await using var db = InMemoryDb.NewContext();
        var ct = TestContext.Current.CancellationToken;
        var time = new FakeTimeProvider(new DateTime(2026, 6, 9, 12, 0, 0, DateTimeKind.Utc));

        db.TucClients.Add(NewClient(77, economyRuns: true, StandardRuns));
        db.TucJobTypes.Add(NewJobType(BaggageSpeed, minutes: 90));
        db.TucJobs.Add(NewJob(4242, 77));
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        var slots = await svc.GetTimeslotsAsync(4242, localDate: new DateTime(2026, 6, 10), ct);

        Assert.Equal("9:00 AM – 10:30 AM", slots[0].Label);
        Assert.Equal("12:30 PM – 2:00 PM", slots[1].Label);
    }

    [Theory]
    // No speed on the job at all, and a speed whose job type has no Minutes —
    // both land on the three-hour default rather than a zero-length window.
    [InlineData(null, null)]
    [InlineData(BaggageSpeed, null)]
    public async Task GetTimeslots_falls_back_to_a_three_hour_window_without_a_speed_duration(
        int? speed, int? minutes)
    {
        await using var db = InMemoryDb.NewContext();
        var ct = TestContext.Current.CancellationToken;
        var time = new FakeTimeProvider(new DateTime(2026, 6, 9, 12, 0, 0, DateTimeKind.Utc));

        db.TucClients.Add(NewClient(77, economyRuns: true, StandardRuns));
        db.TucJobTypes.Add(NewJobType(BaggageSpeed, minutes));
        db.TucJobs.Add(NewJob(4242, 77, speed));
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        var slots = await svc.GetTimeslotsAsync(4242, localDate: new DateTime(2026, 6, 10), ct);

        Assert.Equal("9:00 AM – 12:00 PM", slots[0].Label);
    }

    [Fact]
    public async Task GetTimeslots_dates_every_window_past_tomorrow_without_a_relative_prefix()
    {
        await using var db = InMemoryDb.NewContext();
        var ct = TestContext.Current.CancellationToken;
        var time = new FakeTimeProvider(new DateTime(2026, 6, 9, 12, 0, 0, DateTimeKind.Utc));

        // One run a day, so eight windows span eight business days and step over
        // two weekends.
        db.TucClients.Add(NewClient(77, economyRuns: true, new TimeSpan(9, 0, 0)));
        db.TucJobTypes.Add(NewJobType(BaggageSpeed, minutes: 180));
        db.TucJobs.Add(NewJob(4242, 77));
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        var slots = await svc.GetTimeslotsAsync(4242, localDate: new DateTime(2026, 6, 10), ct);

        Assert.Equal(
        [
            "Today, Wed 10 Jun", "Tomorrow, Thu 11 Jun", "Fri 12 Jun", "Mon 15 Jun",
            "Tue 16 Jun", "Wed 17 Jun", "Thu 18 Jun", "Fri 19 Jun"
        ], slots.Select(s => s.DayLabel));
    }

    [Fact]
    public async Task GetTimeslots_uses_the_runs_of_the_jobs_own_client()
    {
        await using var db = InMemoryDb.NewContext();
        var ct = TestContext.Current.CancellationToken;
        var time = new FakeTimeProvider(new DateTime(2026, 6, 9, 12, 0, 0, DateTimeKind.Utc));

        db.TucClients.Add(NewClient(77, economyRuns: true, StandardRuns));
        db.TucClients.Add(NewClient(88, economyRuns: true, new TimeSpan(7, 0, 0), new TimeSpan(19, 0, 0)));
        db.TucJobTypes.Add(NewJobType(BaggageSpeed, minutes: 180));
        db.TucJobs.Add(NewJob(4242, 77));
        db.TucJobs.Add(NewJob(4243, 88));
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        var first = await svc.GetTimeslotsAsync(4242, new DateTime(2026, 6, 10), ct);
        // Same service instance, so a process-wide run cache would leak client 77
        // into this call.
        var second = await svc.GetTimeslotsAsync(4243, new DateTime(2026, 6, 10), ct);

        Assert.Equal("12:30 PM – 3:30 PM", first[1].Label);
        Assert.Equal("7:00 AM – 10:00 AM", second[0].Label);
        Assert.Equal("7:00 PM – 10:00 PM", second[1].Label);
        // Two runs a day, so the eighth window is four days out.
        Assert.Equal("Mon 15 Jun", second[7].DayLabel);
    }

    [Fact]
    public async Task GetTimeslots_excludes_runs_that_have_already_passed_today()
    {
        await using var db = InMemoryDb.NewContext();
        var ct = TestContext.Current.CancellationToken;
        // 2026-06-10 01:00 UTC = 2026-06-10 13:00 NZ, so 9:00 and 12:30 are gone.
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 1, 0, 0, DateTimeKind.Utc));

        db.TucClients.Add(NewClient(77, economyRuns: true, StandardRuns));
        db.TucJobTypes.Add(NewJobType(BaggageSpeed, minutes: 180));
        db.TucJobs.Add(NewJob(4242, 77));
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        var slots = await svc.GetTimeslotsAsync(4242, localDate: new DateTime(2026, 6, 10), ct);

        Assert.Equal(8, slots.Count);
        Assert.Equal("Today, Wed 10 Jun", slots[0].DayLabel);
        Assert.Equal("3:00 PM – 6:00 PM", slots[0].Label);
        Assert.Equal("5:00 PM – 8:00 PM", slots[1].Label);
        Assert.Equal("Tomorrow, Thu 11 Jun", slots[2].DayLabel);
        Assert.True(slots[0].FirstAvailable);
        Assert.False(slots[1].FirstAvailable);
    }

    [Fact]
    public async Task GetTimeslots_rolls_to_next_business_day_when_every_run_has_passed()
    {
        await using var db = InMemoryDb.NewContext();
        var ct = TestContext.Current.CancellationToken;
        // 2026-06-10 08:00 UTC = 2026-06-10 20:00 NZ — past the 5pm final run.
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 8, 0, 0, DateTimeKind.Utc));

        db.TucClients.Add(NewClient(77, economyRuns: true, StandardRuns));
        db.TucJobTypes.Add(NewJobType(BaggageSpeed, minutes: 180));
        db.TucJobs.Add(NewJob(4242, 77));
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        var slots = await svc.GetTimeslotsAsync(4242, localDate: new DateTime(2026, 6, 10), ct);

        // Thursday the 11th, whole day offered again.
        Assert.Equal(8, slots.Count);
        Assert.Equal("Tomorrow, Thu 11 Jun", slots[0].DayLabel);
        Assert.Equal(new DateTime(2026, 6, 10, 21, 0, 0, DateTimeKind.Utc), slots[0].RunUtc);
        Assert.True(slots[0].FirstAvailable);
    }

    [Fact]
    public async Task GetTimeslots_rolls_forward_when_the_anchor_date_is_not_a_business_day()
    {
        await using var db = InMemoryDb.NewContext();
        var ct = TestContext.Current.CancellationToken;
        var time = new FakeTimeProvider(new DateTime(2026, 6, 12, 12, 0, 0, DateTimeKind.Utc));

        db.TucClients.Add(NewClient(77, economyRuns: true, StandardRuns));
        db.TucJobTypes.Add(NewJobType(BaggageSpeed, minutes: 180));
        db.TucJobs.Add(NewJob(4242, 77));
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        // 2026-06-13 is a Saturday.
        var slots = await svc.GetTimeslotsAsync(4242, localDate: new DateTime(2026, 6, 13), ct);

        // Monday the 15th, every run available because the roll-forward resets
        // the time-of-day comparison. Neither today nor tomorrow, so the label is
        // a bare date even though the anchor was "now".
        Assert.Equal(8, slots.Count);
        Assert.Equal("Mon 15 Jun", slots[0].DayLabel);
        Assert.Equal(new DateTime(2026, 6, 14, 21, 0, 0, DateTimeKind.Utc), slots[0].RunUtc);
    }

    [Fact]
    public async Task GetTimeslots_falls_back_to_baggage_rebook_when_the_client_has_no_runs()
    {
        await using var db = InMemoryDb.NewContext();
        var ct = TestContext.Current.CancellationToken;
        var time = new FakeTimeProvider(new DateTime(2026, 6, 9, 12, 0, 0, DateTimeKind.Utc));

        db.TblEcoSettings.Add(new TblEcoSetting
        {
            SettingId = 1,
            BaggageCutOff = new DateTime(1900, 1, 1, 14, 0, 0),
            BaggageRebook = new DateTime(1900, 1, 1, 10, 0, 0)
        });
        db.TucClients.Add(NewClient(77, economyRuns: true));
        db.TucJobs.Add(NewJob(4242, 77));
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        var slots = await svc.GetTimeslotsAsync(4242, localDate: new DateTime(2026, 6, 10), ct);

        // NZ-local 00:00 is before the 14:00 cutoff, so the rebook time stands today,
        // then repeats on each of the next seven business days.
        Assert.Equal(8, slots.Count);
        Assert.Equal("Today, Wed 10 Jun", slots[0].DayLabel);
        Assert.Equal("10:00 AM – 1:00 PM", slots[0].Label);
        Assert.Equal(new DateTime(2026, 6, 9, 22, 0, 0, DateTimeKind.Utc), slots[0].RunUtc);
        Assert.True(slots[0].FirstAvailable);
        Assert.Equal("Tomorrow, Thu 11 Jun", slots[1].DayLabel);
        Assert.Equal("Fri 19 Jun", slots[7].DayLabel);
    }

    [Fact]
    public async Task GetTimeslots_falls_back_to_next_business_day_when_past_the_baggage_cutoff()
    {
        await using var db = InMemoryDb.NewContext();
        var ct = TestContext.Current.CancellationToken;
        // 2026-06-10 06:00 UTC = 18:00 NZ, past the 14:00 cutoff.
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 6, 0, 0, DateTimeKind.Utc));

        db.TblEcoSettings.Add(new TblEcoSetting
        {
            SettingId = 1,
            BaggageCutOff = new DateTime(1900, 1, 1, 14, 0, 0),
            BaggageRebook = new DateTime(1900, 1, 1, 10, 0, 0)
        });
        db.TucClients.Add(NewClient(77, economyRuns: true));
        db.TucJobs.Add(NewJob(4242, 77));
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        var slots = await svc.GetTimeslotsAsync(4242, localDate: new DateTime(2026, 6, 10), ct);

        Assert.Equal(8, slots.Count);
        Assert.Equal("Tomorrow, Thu 11 Jun", slots[0].DayLabel);
        Assert.Equal(new DateTime(2026, 6, 10, 22, 0, 0, DateTimeKind.Utc), slots[0].RunUtc);
    }

    [Fact]
    public async Task GetTimeslots_falls_back_when_the_economy_runs_flag_is_off()
    {
        await using var db = InMemoryDb.NewContext();
        var ct = TestContext.Current.CancellationToken;
        var time = new FakeTimeProvider(new DateTime(2026, 6, 9, 12, 0, 0, DateTimeKind.Utc));

        db.TblEcoSettings.Add(new TblEcoSetting
        {
            SettingId = 1,
            BaggageCutOff = new DateTime(1900, 1, 1, 14, 0, 0),
            BaggageRebook = new DateTime(1900, 1, 1, 10, 0, 0)
        });
        // Runs are configured, but the client isn't opted in to run-based delivery.
        db.TucClients.Add(NewClient(77, economyRuns: false, StandardRuns));
        db.TucJobs.Add(NewJob(4242, 77));
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        var slots = await svc.GetTimeslotsAsync(4242, localDate: new DateTime(2026, 6, 10), ct);

        Assert.Equal(8, slots.Count);
        Assert.Equal("10:00 AM – 1:00 PM", slots[0].Label);
    }

    [Fact]
    public async Task GetTimeslots_returns_empty_when_the_job_is_unknown()
    {
        await using var db = InMemoryDb.NewContext();
        var ct = TestContext.Current.CancellationToken;
        var time = new FakeTimeProvider(new DateTime(2026, 6, 9, 12, 0, 0, DateTimeKind.Utc));

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        Assert.Empty(await svc.GetTimeslotsAsync(9999, new DateTime(2026, 6, 10), ct));
    }

    [Fact]
    public async Task GetTimeslots_returns_empty_when_there_are_no_runs_and_no_eco_setting_row()
    {
        await using var db = InMemoryDb.NewContext();
        var ct = TestContext.Current.CancellationToken;
        var time = new FakeTimeProvider(new DateTime(2026, 6, 9, 12, 0, 0, DateTimeKind.Utc));

        db.TucClients.Add(NewClient(77, economyRuns: true));
        db.TucJobs.Add(NewJob(4242, 77));
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        Assert.Empty(await svc.GetTimeslotsAsync(4242, new DateTime(2026, 6, 10), ct));
    }

    [Fact]
    public async Task GetTimeslots_sorts_runs_that_are_configured_out_of_column_order()
    {
        await using var db = InMemoryDb.NewContext();
        var ct = TestContext.Current.CancellationToken;
        var time = new FakeTimeProvider(new DateTime(2026, 6, 9, 12, 0, 0, DateTimeKind.Utc));

        db.TucClients.Add(NewClient(77, economyRuns: true,
            new TimeSpan(15, 0, 0), new TimeSpan(9, 0, 0), new TimeSpan(12, 0, 0)));
        db.TucJobs.Add(NewJob(4242, 77));
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        var slots = await svc.GetTimeslotsAsync(4242, new DateTime(2026, 6, 10), ct);

        Assert.Equal(
            ["9:00 AM – 12:00 PM", "12:00 PM – 3:00 PM", "3:00 PM – 6:00 PM"],
            slots.Take(3).Select(s => s.Label));
    }

    [Fact]
    public async Task Confirm_updates_tucJob_and_writes_baggage_delivery_booking_journey_row()
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 3, 0, 0, DateTimeKind.Utc));

        var ct = TestContext.Current.CancellationToken;
        db.TblJobLeaveNotHomes.Add(new TblJobLeaveNotHome
        {
            LeaveNotHomeId = 5, Name = "Front door", Smsname = "door", Category = "All",
            AllowLeave = true, Sequence = 1, CreatedBy = "test", LastModifiedBy = "test"
        });
        db.TucJobs.Add(new TucJob
        {
            UcjbId = 4242,
            UcjbNumber = "TEST-4242",
            DeliverToContact = "old name",
            DeliveryAddressLine1 = "Acme Co",
            DeliveryAddressLine2 = "Building B",
            UcjbStatus = (int)JobStatus.Dispatched
        });
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        await svc.ConfirmAsync(new ConfirmBookingInput(
            JobId: 4242,
            Address: new AddressUpdateDto
            {
                Line1 = "1 Test Street",
                Suburb = "Ponsonby",
                City = "Auckland",
                PostCode = "1011",
                Country = "NZ"
            },
            DeliveryTimeUtc: new DateTime(2026, 6, 11, 0, 0, 0, DateTimeKind.Utc),
            AtlOptionId: 5,
            AccessNotes: "Buzzer 3",
            PassengerName: "Jane Pax",
            PassengerPhone: "+64 21 000",
            PassengerEmail: "jane@example.com"), ct);

        var job = await db.TucJobs.AsNoTracking().SingleAsync(j => j.UcjbId == 4242, ct);
        Assert.Equal("Jane Pax", job.DeliverToContact);
        Assert.Equal((int)JobStatus.New, job.UcjbStatus);
        Assert.Equal(5, job.DeliverToLeaveId);

        // Canonical Despatch address columns: combined street → L4 (L3 cleared),
        // suburb → L5, city → L6, postcode → L7, country → L8. Company (L1) and
        // building (L2) are left untouched by the pax edit.
        Assert.Equal("Acme Co", job.DeliveryAddressLine1);
        Assert.Equal("Building B", job.DeliveryAddressLine2);
        Assert.Null(job.DeliveryAddressLine3);
        Assert.Equal("1 Test Street", job.DeliveryAddressLine4);
        Assert.Equal("Ponsonby", job.DeliveryAddressLine5);
        Assert.Equal("Auckland", job.DeliveryAddressLine6);
        Assert.Equal("1011", job.DeliveryAddressLine7);
        Assert.Equal("NZ", job.DeliveryAddressLine8);

        var journey = await db.JobDeliveryJourneys.AsNoTracking().SingleAsync(j => j.JobId == 4242, ct);
        Assert.Equal(nameof(DeliveryJourneyChangeType.BaggageDeliveryBooking), journey.ChangeType);
        Assert.Equal(nameof(DeliveryJourneyUpdatedByType.System), journey.UpdatedByType);
        Assert.Equal(time.GetUtcNow().UtcDateTime, journey.UpdatedAt);
        Assert.Contains("self-service", journey.Comments);
    }

    [Fact]
    public async Task Confirm_persists_null_leave_id_when_atl_option_is_off()
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 3, 0, 0, DateTimeKind.Utc));

        var ct = TestContext.Current.CancellationToken;
        db.TblJobLeaveNotHomes.Add(new TblJobLeaveNotHome
        {
            LeaveNotHomeId = 9, Name = "Front door", Smsname = "door", Category = "All",
            AllowLeave = true, Sequence = 1, CreatedBy = "test", LastModifiedBy = "test"
        });
        db.TucJobs.Add(new TucJob
        {
            UcjbId = 4243,
            UcjbNumber = "TEST-4243",
            DeliverToLeaveId = 9,
            UcjbStatus = (int)JobStatus.Dispatched
        });
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        await svc.ConfirmAsync(new ConfirmBookingInput(
            JobId: 4243,
            Address: new AddressUpdateDto { Line1 = "1 Test Street", City = "Auckland", Country = "NZ" },
            DeliveryTimeUtc: new DateTime(2026, 6, 11, 0, 0, 0, DateTimeKind.Utc),
            AtlOptionId: null,
            AccessNotes: null,
            PassengerName: "Jane Pax",
            PassengerPhone: "+64 21 000",
            PassengerEmail: "jane@example.com"), ct);

        var job = await db.TucJobs.AsNoTracking().SingleAsync(j => j.UcjbId == 4243, ct);
        Assert.Null(job.DeliverToLeaveId);
    }

    [Fact]
    public async Task Confirm_throws_when_tucJob_does_not_exist()
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 3, 0, 0, DateTimeKind.Utc));
        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());
        var ct = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ConfirmAsync(new ConfirmBookingInput(
            JobId: 9999,
            Address: new AddressUpdateDto { Line1 = "x", City = "y", Country = "NZ" },
            DeliveryTimeUtc: new DateTime(2026, 6, 11, 0, 0, 0, DateTimeKind.Utc),
            AtlOptionId: null,
            AccessNotes: null,
            PassengerName: "X",
            PassengerPhone: null,
            PassengerEmail: null), ct));

        Assert.False(await db.JobDeliveryJourneys.AnyAsync(ct));
    }

    [Fact]
    public async Task GetSummary_returns_only_category_all_allow_leave_atl_options_ordered_by_sequence_desc_then_name()
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc));
        var ct = TestContext.Current.CancellationToken;

        db.TucJobs.Add(new TucJob { UcjbId = 7, UcjbNumber = "JOB-7" });
        // Production Category sentinel for "all categories" is "All," (trailing
        // comma); other/null categories are excluded.
        db.TblJobLeaveNotHomes.AddRange(
            new TblJobLeaveNotHome { LeaveNotHomeId = 1, Name = "Front porch", Smsname = "porch", Category = "All,", AllowLeave = true, Sequence = 2, CreatedBy = "test", LastModifiedBy = "test" },
            new TblJobLeaveNotHome { LeaveNotHomeId = 2, Name = "Back door", Smsname = "door", Category = "All,", AllowLeave = false, Sequence = 1, CreatedBy = "test", LastModifiedBy = "test" },
            new TblJobLeaveNotHome { LeaveNotHomeId = 3, Name = "With neighbour", Smsname = "neighbour", Category = "Commercial", AllowLeave = true, Sequence = 0, CreatedBy = "test", LastModifiedBy = "test" },
            new TblJobLeaveNotHome { LeaveNotHomeId = 4, Name = "Garage", Smsname = "garage", Category = "All,", AllowLeave = true, Sequence = 5, CreatedBy = "test", LastModifiedBy = "test" });
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        var summary = await svc.GetSummaryAsync(7, ct);

        Assert.NotNull(summary);
        // Highest sequence first; "Back door" excluded (AllowLeave false),
        // "With neighbour" excluded (wrong category).
        Assert.Equal(["Garage", "Front porch"], summary.AtlOptions.Select(o => o.Name));
        Assert.Equal([4, 1], summary.AtlOptions.Select(o => o.Id));
    }

    [Fact]
    public async Task GetSummary_populates_delivery_address_from_stored_address_lines()
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc));
        var ct = TestContext.Current.CancellationToken;

        db.TucJobs.Add(new TucJob
        {
            UcjbId = 7,
            UcjbNumber = "JOB-7",
            // Canonical Despatch convention: L3=street number, L4=street name,
            // L5=suburb (non-US), L6=city (non-US), L7=postcode, L8=country.
            DeliveryAddressLine3 = "12",
            DeliveryAddressLine4 = "Queen St",
            DeliveryAddressLine5 = "CBD",
            DeliveryAddressLine6 = "Auckland",
            DeliveryAddressLine7 = "1010",
            DeliveryAddressLine8 = "NZ",
            DeliveryLatitude = -36.8485m,
            DeliveryLongitude = 174.7633m
        });
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        var summary = await svc.GetSummaryAsync(7, ct);

        Assert.NotNull(summary);
        var addr = summary.DeliveryAddress;
        Assert.Equal("12 Queen St", addr.Line1);
        Assert.Equal("CBD", addr.Suburb);
        Assert.Equal("Auckland", addr.City);
        Assert.Equal("1010", addr.PostCode);
        Assert.Equal("NZ", addr.Country);
        Assert.Equal(-36.8485m, addr.Latitude);
        Assert.Equal(174.7633m, addr.Longitude);
    }

    [Fact]
    public async Task GetSummary_returns_the_baggage_file_reference_not_the_job_id()
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc));
        var ct = TestContext.Current.CancellationToken;

        db.TucJobs.Add(new TucJob
        {
            UcjbId = 179252, UcjbNumber = "JOB-179252", UcjbClientRefa = " AKLA2633476 "
        });
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        var summary = await svc.GetSummaryAsync(179252, ct);

        Assert.NotNull(summary);
        Assert.Equal("AKLA2633476", summary.Reference);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetSummary_returns_an_empty_reference_when_the_job_has_no_file_reference(
        string? refa)
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc));
        var ct = TestContext.Current.CancellationToken;

        db.TucJobs.Add(new TucJob { UcjbId = 7, UcjbNumber = "JOB-7", UcjbClientRefa = refa });
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        var summary = await svc.GetSummaryAsync(7, ct);

        // The portal hides the reference badge on empty rather than showing a job id
        // the passenger can't quote to the helpline.
        Assert.NotNull(summary);
        Assert.Equal(string.Empty, summary.Reference);
    }

    [Fact]
    public async Task GetSummary_extracts_airline_code_from_worldtracer_ref()
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc));
        var ct = TestContext.Current.CancellationToken;

        // WorldTracer file ref: station(3) + airline(2) + sequence -> "NZ".
        db.TucJobs.Add(new TucJob { UcjbId = 7, UcjbNumber = "JOB-7", UcjbClientRefa = "AKLNZ12345" });
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        var summary = await svc.GetSummaryAsync(7, ct);

        Assert.NotNull(summary);
        Assert.Equal("NZ", summary.AirlineCode);
    }

    [Fact]
    public async Task GetSummary_prefers_worldtracer_ref_over_job_client_code()
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc));
        var ct = TestContext.Current.CancellationToken;

        db.TucJobs.Add(new TucJob
        {
            UcjbId = 7, UcjbNumber = "JOB-7", UcjbClientRefa = "AKLNZ12345", UcjbClientCode = "QF"
        });
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        var summary = await svc.GetSummaryAsync(7, ct);

        Assert.NotNull(summary);
        Assert.Equal("NZ", summary.AirlineCode);
    }

    [Fact]
    public async Task GetSummary_falls_back_to_job_client_code_when_ref_not_worldtracer()
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc));
        var ct = TestContext.Current.CancellationToken;

        // Too short / no two-letter airline at positions 4-5 -> not a WT ref.
        db.TucJobs.Add(new TucJob
        {
            UcjbId = 7, UcjbNumber = "JOB-7", UcjbClientRefa = "AK12345", UcjbClientCode = "QF"
        });
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        var summary = await svc.GetSummaryAsync(7, ct);

        Assert.NotNull(summary);
        Assert.Equal("QF", summary.AirlineCode);
    }

    [Fact]
    public async Task GetSummary_offers_the_clients_own_phone_as_the_support_number()
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc));
        var ct = TestContext.Current.CancellationToken;

        var client = NewClient(77, economyRuns: false);
        client.UcclPhone = " 0800 267 5494 ";
        db.TucClients.Add(client);
        db.TucJobs.Add(NewJob(7, 77));
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts("0800 111 222"), NewCache(), time,
            NewCalendar());

        var summary = await svc.GetSummaryAsync(7, ct);

        // The airline the passenger flew with beats the courier's own line.
        Assert.NotNull(summary);
        Assert.Equal("0800 267 5494", summary.SupportPhone);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetSummary_falls_back_to_the_tenant_support_phone(string? clientPhone)
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc));
        var ct = TestContext.Current.CancellationToken;

        var client = NewClient(77, economyRuns: false);
        client.UcclPhone = clientPhone;
        db.TucClients.Add(client);
        db.TucJobs.Add(NewJob(7, 77));
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts("0800 111 222"), NewCache(), time,
            NewCalendar());

        var summary = await svc.GetSummaryAsync(7, ct);

        Assert.NotNull(summary);
        Assert.Equal("0800 111 222", summary.SupportPhone);
    }

    [Fact]
    public async Task GetSummary_returns_an_empty_support_phone_when_neither_is_configured()
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc));
        var ct = TestContext.Current.CancellationToken;

        db.TucJobs.Add(new TucJob { UcjbId = 7, UcjbNumber = "JOB-7" });
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        var summary = await svc.GetSummaryAsync(7, ct);

        // The portal drops the "Need help?" line rather than printing a dead prompt.
        Assert.NotNull(summary);
        Assert.Equal(string.Empty, summary.SupportPhone);
    }

    [Fact]
    public async Task GetSummary_surfaces_airline_code_from_job_client_code()
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc));
        var ct = TestContext.Current.CancellationToken;

        db.TucJobs.Add(new TucJob { UcjbId = 7, UcjbNumber = "JOB-7", UcjbClientCode = "QF" });
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        var summary = await svc.GetSummaryAsync(7, ct);

        Assert.NotNull(summary);
        Assert.Equal("QF", summary.AirlineCode);
    }

    [Fact]
    public async Task GetSummary_falls_back_to_client_code_when_job_client_code_blank()
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc));
        var ct = TestContext.Current.CancellationToken;

        db.TucClients.Add(new TucClient
        {
            UcclId = 3, UcclCode = "NZ", UcclName = "Air New Zealand",
            UcclLegalName = "Air New Zealand Ltd", Smsname = "AirNZ",
            CreatedBy = "test", LastModifiedBy = "test"
        });
        db.TucJobs.Add(new TucJob { UcjbId = 7, UcjbNumber = "JOB-7", UcjbClientId = 3 });
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        var summary = await svc.GetSummaryAsync(7, ct);

        Assert.NotNull(summary);
        Assert.Equal("NZ", summary.AirlineCode);
    }

    [Fact]
    public async Task GetSummary_falls_back_to_default_country_when_no_address_lines_stored()
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc));
        var ct = TestContext.Current.CancellationToken;

        db.TucJobs.Add(new TucJob { UcjbId = 7, UcjbNumber = "JOB-7" });
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        var summary = await svc.GetSummaryAsync(7, ct);

        Assert.NotNull(summary);
        var addr = summary.DeliveryAddress;
        Assert.Equal(string.Empty, addr.Line1);
        Assert.Equal(string.Empty, addr.City);
        Assert.Equal("NZ", addr.Country);
        Assert.Null(addr.Latitude);
        Assert.Null(addr.Longitude);
    }

    [Theory]
    // DeliveryAddressLine8 is legacy free text, not a validated ISO-2 code.
    [InlineData("New Zealand", "NZ")]
    [InlineData("NEW ZEALAND", "NZ")]
    [InlineData("NZL", "NZ")]
    [InlineData(" nz ", "NZ")]
    public async Task GetSummary_normalises_legacy_country_to_iso2(string line8, string expected)
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc));
        var ct = TestContext.Current.CancellationToken;

        db.TucJobs.Add(new TucJob
        {
            UcjbId = 7,
            UcjbNumber = "JOB-7",
            DeliveryAddressLine4 = "Queen St",
            DeliveryAddressLine5 = "CBD",
            DeliveryAddressLine6 = "Auckland",
            DeliveryAddressLine8 = line8
        });
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        var summary = await svc.GetSummaryAsync(7, ct);

        Assert.NotNull(summary);
        Assert.Equal(expected, summary.DeliveryAddress.Country);
    }

    [Fact]
    public async Task GetSummary_returns_empty_country_when_line8_is_unresolvable()
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc));
        var ct = TestContext.Current.CancellationToken;

        db.TucJobs.Add(new TucJob
        {
            UcjbId = 7, UcjbNumber = "JOB-7", DeliveryAddressLine8 = "Wakanda"
        });
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        var summary = await svc.GetSummaryAsync(7, ct);

        // Empty is the signal the pax portal uses to force an address re-selection
        // through the search — we don't silently guess the tenant default here.
        Assert.NotNull(summary);
        Assert.Equal(string.Empty, summary.DeliveryAddress.Country);
    }

    [Fact]
    public async Task GetSummary_treats_legacy_united_states_spelling_as_us_layout()
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc));
        var ct = TestContext.Current.CancellationToken;

        db.TucJobs.Add(new TucJob
        {
            UcjbId = 7,
            UcjbNumber = "JOB-7",
            DeliveryAddressLine4 = "5th Ave",
            // US convention: L5 is the city, there is no suburb line.
            DeliveryAddressLine5 = "New York",
            DeliveryAddressLine8 = "United States of America"
        });
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        var summary = await svc.GetSummaryAsync(7, ct);

        Assert.NotNull(summary);
        var addr = summary.DeliveryAddress;
        Assert.Equal("US", addr.Country);
        Assert.Equal("New York", addr.City);
        Assert.Null(addr.Suburb);
    }

    [Fact]
    public async Task Confirm_persists_normalised_iso2_country()
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 3, 0, 0, DateTimeKind.Utc));
        var ct = TestContext.Current.CancellationToken;

        db.TucJobs.Add(new TucJob { UcjbId = 4242, UcjbNumber = "TEST-4242" });
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        await svc.ConfirmAsync(new ConfirmBookingInput(
            JobId: 4242,
            Address: new AddressUpdateDto
            {
                Line1 = "1 Test Street",
                Suburb = "Ponsonby",
                City = "Auckland",
                PostCode = "1011",
                Country = "New Zealand"
            },
            DeliveryTimeUtc: new DateTime(2026, 6, 11, 0, 0, 0, DateTimeKind.Utc),
            AtlOptionId: null,
            AccessNotes: null,
            PassengerName: "Jane Pax",
            PassengerPhone: "+64 21 000",
            PassengerEmail: "jane@example.com"), ct);

        var job = await db.TucJobs.AsNoTracking().SingleAsync(j => j.UcjbId == 4242, ct);
        Assert.Equal("NZ", job.DeliveryAddressLine8);
    }

    [Fact]
    public async Task Confirm_uses_us_column_layout_for_legacy_us_spelling()
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 3, 0, 0, DateTimeKind.Utc));
        var ct = TestContext.Current.CancellationToken;

        db.TucJobs.Add(new TucJob { UcjbId = 4242, UcjbNumber = "TEST-4242" });
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        await svc.ConfirmAsync(new ConfirmBookingInput(
            JobId: 4242,
            Address: new AddressUpdateDto
            {
                Line1 = "350 5th Ave",
                Suburb = "Brooklyn",
                City = "New York",
                PostCode = "10118",
                Country = "United States"
            },
            DeliveryTimeUtc: new DateTime(2026, 6, 11, 0, 0, 0, DateTimeKind.Utc),
            AtlOptionId: null,
            AccessNotes: null,
            PassengerName: "Jane Pax",
            PassengerPhone: "+1 555 0100",
            PassengerEmail: "jane@example.com"), ct);

        var job = await db.TucJobs.AsNoTracking().SingleAsync(j => j.UcjbId == 4242, ct);
        Assert.Equal("US", job.DeliveryAddressLine8);
        Assert.Equal("New York", job.DeliveryAddressLine5);
        Assert.Null(job.DeliveryAddressLine6);
    }

    [Fact]
    public async Task GetSummary_then_Confirm_round_trips_legacy_country()
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 3, 0, 0, DateTimeKind.Utc));
        var ct = TestContext.Current.CancellationToken;

        db.TucJobs.Add(new TucJob
        {
            UcjbId = 4242,
            UcjbNumber = "TEST-4242",
            DeliveryAddressLine4 = "Queen St",
            DeliveryAddressLine5 = "CBD",
            DeliveryAddressLine6 = "Auckland",
            DeliveryAddressLine7 = "1010",
            DeliveryAddressLine8 = "New Zealand"
        });
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        var summary = await svc.GetSummaryAsync(4242, ct);
        Assert.NotNull(summary);

        // Exactly what the pax portal posts back when the passenger only picks a
        // timeslot: the address object it was handed, untouched.
        await svc.ConfirmAsync(new ConfirmBookingInput(
            JobId: 4242,
            Address: summary.DeliveryAddress,
            DeliveryTimeUtc: new DateTime(2026, 6, 11, 0, 0, 0, DateTimeKind.Utc),
            AtlOptionId: null,
            AccessNotes: null,
            PassengerName: "Jane Pax",
            PassengerPhone: "+64 21 000",
            PassengerEmail: "jane@example.com"), ct);

        var job = await db.TucJobs.AsNoTracking().SingleAsync(j => j.UcjbId == 4242, ct);
        Assert.Equal("NZ", job.DeliveryAddressLine8);
        Assert.Equal("Queen St", job.DeliveryAddressLine4);
        Assert.Equal("CBD", job.DeliveryAddressLine5);
        Assert.Equal("Auckland", job.DeliveryAddressLine6);
    }

}
