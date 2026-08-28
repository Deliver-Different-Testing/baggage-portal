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
    private static IOptions<DespatchOptions> DespatchOpts(
        string supportPhone = "",
        LeaveNotHomeOption[]? excludedAtlOptions = null,
        LeaveNotHomeOption defaultAtlOption = LeaveNotHomeOption.FrontDoor,
        string timeZone = "Pacific/Auckland") =>
        Options.Create(new DespatchOptions
        {
            TimeZone = timeZone,
            SupportPhone = supportPhone,
            ExcludedAtlOptions = excludedAtlOptions ?? [LeaveNotHomeOption.LetterBox],
            DefaultAtlOption = defaultAtlOption
        });

    private static TblJobLeaveNotHome NewAtlOption(LeaveNotHomeOption id, string name, int sequence) =>
        new()
        {
            LeaveNotHomeId = (int)id, Name = name, Smsname = name, Category = "All,",
            AllowLeave = true, Sequence = sequence, CreatedBy = "test", LastModifiedBy = "test"
        };

    private static MemoryCache NewCache() => new(new MemoryCacheOptions());

    private sealed class FakeCalendar(params DateTime[] extraNonBusinessDays) : IDespatchCalendar
    {
        public int NextBusinessDaysCalls { get; private set; }

        public int AddBusinessDaysCalls { get; private set; }

        private bool IsBusiness(DateTime d) =>
            d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)
            && !extraNonBusinessDays.Contains(d.Date);

        public Task<bool> IsBusinessDayAsync(DateTime localDate, int clientId, CancellationToken ct) =>
            Task.FromResult(IsBusiness(localDate.Date));

        public Task<DateTime> AddBusinessDaysAsync(int days, DateTime localDate, int clientId,
            CancellationToken ct)
        {
            AddBusinessDaysCalls++;
            return Task.FromResult(Walk(localDate, days));
        }

        public Task<IReadOnlyList<DateTime>> NextBusinessDaysAsync(int count, DateTime localDate,
            int clientId, CancellationToken ct)
        {
            NextBusinessDaysCalls++;
            var days = new List<DateTime>(count);
            var d = localDate.Date;
            for (var i = 0; i < count; i++)
            {
                d = Walk(d, 1);
                days.Add(d);
            }

            return Task.FromResult<IReadOnlyList<DateTime>>(days);
        }

        private DateTime Walk(DateTime from, int days)
        {
            var d = from.Date;
            for (var i = 0; i < days; i++)
            {
                do
                {
                    d = d.AddDays(1);
                } while (!IsBusiness(d));
            }

            return d;
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

    private static void AddJob(BaggageDeliveryContext db, int jobId, int clientId,
        int? speed = BaggageSpeed)
    {
        if (speed is { } speedId && db.TucJobTypes.Local.All(t => t.UcjtId != speedId))
        {
            db.TucJobTypes.Add(NewJobType(speedId, minutes: null));
        }

        db.TucJobs.Add(new TucJob
        {
            UcjbId = jobId, UcjbNumber = $"JOB-{jobId}", UcjbClientId = clientId, UcjbSpeed = speed
        });
    }

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
    public async Task GetTimeslots_walks_the_business_day_chain_in_one_batched_call()
    {
        await using var db = InMemoryDb.NewContext();
        var ct = TestContext.Current.CancellationToken;
        var time = new FakeTimeProvider(new DateTime(2026, 6, 9, 12, 0, 0, DateTimeKind.Utc));

        db.TucClients.Add(NewClient(77, economyRuns: true, new TimeSpan(9, 0, 0)));
        db.TucJobTypes.Add(NewJobType(BaggageSpeed, minutes: 180));
        AddJob(db, 4242, 77);
        await db.SaveChangesAsync(ct);

        var calendar = NewCalendar();
        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, calendar);

        var slots = await svc.GetTimeslotsAsync(4242, localDate: new DateTime(2026, 6, 10), ct);

        Assert.Equal(8, slots.Count);
        Assert.Equal(1, calendar.NextBusinessDaysCalls);
        Assert.Equal(0, calendar.AddBusinessDaysCalls);
        Assert.Equal("Today, Wed 10 Jun", slots[0].DayLabel);
        Assert.Equal("Fri 19 Jun", slots[7].DayLabel);
    }

    [Fact]
    public async Task GetTimeslots_returns_eight_windows_rolling_into_the_next_business_day()
    {
        await using var db = InMemoryDb.NewContext();
        var ct = TestContext.Current.CancellationToken;
        var time = new FakeTimeProvider(new DateTime(2026, 6, 9, 12, 0, 0, DateTimeKind.Utc));

        db.TucClients.Add(NewClient(77, economyRuns: true, StandardRuns));
        db.TucJobTypes.Add(NewJobType(BaggageSpeed, minutes: 180));
        AddJob(db, 4242, 77);
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        var slots = await svc.GetTimeslotsAsync(4242, localDate: new DateTime(2026, 6, 10), ct);

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
        AddJob(db, 4242, 77);
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        var slots = await svc.GetTimeslotsAsync(4242, localDate: new DateTime(2026, 6, 10), ct);

        Assert.Equal("9:00 AM – 10:30 AM", slots[0].Label);
        Assert.Equal("12:30 PM – 2:00 PM", slots[1].Label);
    }

    [Theory]
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
        AddJob(db, 4242, 77, speed);
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

        db.TucClients.Add(NewClient(77, economyRuns: true, new TimeSpan(9, 0, 0)));
        db.TucJobTypes.Add(NewJobType(BaggageSpeed, minutes: 180));
        AddJob(db, 4242, 77);
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
        AddJob(db, 4242, 77);
        AddJob(db, 4243, 88);
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        var first = await svc.GetTimeslotsAsync(4242, new DateTime(2026, 6, 10), ct);
        var second = await svc.GetTimeslotsAsync(4243, new DateTime(2026, 6, 10), ct);

        Assert.Equal("12:30 PM – 3:30 PM", first[1].Label);
        Assert.Equal("7:00 AM – 10:00 AM", second[0].Label);
        Assert.Equal("7:00 PM – 10:00 PM", second[1].Label);
        Assert.Equal("Mon 15 Jun", second[7].DayLabel);
    }

    [Fact]
    public async Task GetTimeslots_excludes_runs_that_have_already_passed_today()
    {
        await using var db = InMemoryDb.NewContext();
        var ct = TestContext.Current.CancellationToken;
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 1, 0, 0, DateTimeKind.Utc));

        db.TucClients.Add(NewClient(77, economyRuns: true, StandardRuns));
        db.TucJobTypes.Add(NewJobType(BaggageSpeed, minutes: 180));
        AddJob(db, 4242, 77);
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
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 8, 0, 0, DateTimeKind.Utc));

        db.TucClients.Add(NewClient(77, economyRuns: true, StandardRuns));
        db.TucJobTypes.Add(NewJobType(BaggageSpeed, minutes: 180));
        AddJob(db, 4242, 77);
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        var slots = await svc.GetTimeslotsAsync(4242, localDate: new DateTime(2026, 6, 10), ct);

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
        AddJob(db, 4242, 77);
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        var slots = await svc.GetTimeslotsAsync(4242, localDate: new DateTime(2026, 6, 13), ct);

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
        AddJob(db, 4242, 77);
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        var slots = await svc.GetTimeslotsAsync(4242, localDate: new DateTime(2026, 6, 10), ct);

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
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 6, 0, 0, DateTimeKind.Utc));

        db.TblEcoSettings.Add(new TblEcoSetting
        {
            SettingId = 1,
            BaggageCutOff = new DateTime(1900, 1, 1, 14, 0, 0),
            BaggageRebook = new DateTime(1900, 1, 1, 10, 0, 0)
        });
        db.TucClients.Add(NewClient(77, economyRuns: true));
        AddJob(db, 4242, 77);
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
        db.TucClients.Add(NewClient(77, economyRuns: false, StandardRuns));
        AddJob(db, 4242, 77);
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
        AddJob(db, 4242, 77);
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
        AddJob(db, 4242, 77);
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
                Line1 = "Acme Co",
                Line2 = "Apartment 4B, ring the buzzer",
                Line3 = "1",
                Line4 = "Test Street",
                Line5 = "Ponsonby",
                Line6 = "Auckland",
                Line7 = "1011",
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

        Assert.Equal("Acme Co", job.DeliveryAddressLine1);
        Assert.Equal("Apartment 4B, ring the buzzer", job.DeliveryAddressLine2);
        Assert.Equal("1", job.DeliveryAddressLine3);
        Assert.Equal("Test Street", job.DeliveryAddressLine4);
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
            Address: new AddressUpdateDto { Line4 = "Test Street", Line5 = "Ponsonby", Line6 = "Auckland", Country = "NZ" },
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
    public async Task Confirm_clears_extra_delivery_information_the_passenger_emptied()
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 3, 0, 0, DateTimeKind.Utc));

        var ct = TestContext.Current.CancellationToken;
        db.TucJobs.Add(new TucJob
        {
            UcjbId = 4244,
            UcjbNumber = "TEST-4244",
            DeliveryAddressLine2 = "Building B",
            UcjbStatus = (int)JobStatus.Dispatched
        });
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        await svc.ConfirmAsync(new ConfirmBookingInput(
            JobId: 4244,
            Address: new AddressUpdateDto
            {
                Line2 = null, Line4 = "Test Street", Line5 = "Ponsonby", Line6 = "Auckland",
                Country = "NZ"
            },
            DeliveryTimeUtc: new DateTime(2026, 6, 11, 0, 0, 0, DateTimeKind.Utc),
            AtlOptionId: null,
            AccessNotes: null,
            PassengerName: "Jane Pax",
            PassengerPhone: "+64 21 000",
            PassengerEmail: "jane@example.com"), ct);

        var job = await db.TucJobs.AsNoTracking().SingleAsync(j => j.UcjbId == 4244, ct);
        Assert.Null(job.DeliveryAddressLine2);
    }

    [Fact]
    public async Task Confirm_books_the_job_onto_the_start_of_the_selected_window()
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 3, 0, 0, DateTimeKind.Utc));

        var ct = TestContext.Current.CancellationToken;
        db.TucJobs.Add(new TucJob
        {
            UcjbId = 4250,
            UcjbNumber = "TEST-4250",
            UcjbDate = new DateTime(2026, 6, 1, 0, 0, 0),
            UcjbTime = new DateTime(2026, 6, 1, 14, 30, 0),
            UcjbStatus = (int)JobStatus.Dispatched
        });
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        // 2026-06-10T21:00Z is 09:00 on 11 June in Pacific/Auckland (NZST).
        await svc.ConfirmAsync(new ConfirmBookingInput(
            JobId: 4250,
            Address: new AddressUpdateDto { Line4 = "Test Street", Line5 = "Ponsonby", Line6 = "Auckland", Country = "NZ" },
            DeliveryTimeUtc: new DateTime(2026, 6, 10, 21, 0, 0, DateTimeKind.Utc),
            AtlOptionId: null,
            AccessNotes: null,
            PassengerName: "Jane Pax",
            PassengerPhone: null,
            PassengerEmail: null), ct);

        var job = await db.TucJobs.AsNoTracking().SingleAsync(j => j.UcjbId == 4250, ct);
        Assert.Equal(new DateTime(2026, 6, 11, 0, 0, 0), job.UcjbDate);
        Assert.Equal(new DateTime(2026, 6, 11, 9, 0, 0), job.UcjbTime);
        Assert.Equal(new DateTime(2026, 6, 11, 9, 0, 0), job.DeliverByTime);
    }

    [Fact]
    public async Task Confirm_leaves_the_booked_date_untouched_when_the_tenant_timezone_is_unresolved()
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 3, 0, 0, DateTimeKind.Utc));

        var ct = TestContext.Current.CancellationToken;
        db.TucJobs.Add(new TucJob
        {
            UcjbId = 4251,
            UcjbNumber = "TEST-4251",
            UcjbDate = new DateTime(2026, 6, 1, 0, 0, 0),
            UcjbTime = new DateTime(2026, 6, 1, 14, 30, 0),
            UcjbStatus = (int)JobStatus.Dispatched
        });
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(timeZone: ""), NewCache(), time, NewCalendar());

        await svc.ConfirmAsync(new ConfirmBookingInput(
            JobId: 4251,
            Address: new AddressUpdateDto { Line4 = "Test Street", Line5 = "Ponsonby", Line6 = "Auckland", Country = "NZ" },
            DeliveryTimeUtc: new DateTime(2026, 6, 10, 21, 0, 0, DateTimeKind.Utc),
            AtlOptionId: null,
            AccessNotes: null,
            PassengerName: "Jane Pax",
            PassengerPhone: null,
            PassengerEmail: null), ct);

        var job = await db.TucJobs.AsNoTracking().SingleAsync(j => j.UcjbId == 4251, ct);
        Assert.Equal(new DateTime(2026, 6, 1, 0, 0, 0), job.UcjbDate);
        Assert.Equal(new DateTime(2026, 6, 1, 14, 30, 0), job.UcjbTime);
        Assert.Null(job.DeliverByTime);
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
            Address: new AddressUpdateDto { Line4 = "x", Line5 = "y", Line6 = "z", Country = "NZ" },
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
        db.TblJobLeaveNotHomes.AddRange(
            new TblJobLeaveNotHome { LeaveNotHomeId = 1, Name = "Front porch", Smsname = "porch", Category = "All,", AllowLeave = true, Sequence = 2, CreatedBy = "test", LastModifiedBy = "test" },
            new TblJobLeaveNotHome { LeaveNotHomeId = 2, Name = "Back door", Smsname = "door", Category = "All,", AllowLeave = false, Sequence = 1, CreatedBy = "test", LastModifiedBy = "test" },
            new TblJobLeaveNotHome { LeaveNotHomeId = 3, Name = "With neighbour", Smsname = "neighbour", Category = "Commercial", AllowLeave = true, Sequence = 0, CreatedBy = "test", LastModifiedBy = "test" },
            new TblJobLeaveNotHome { LeaveNotHomeId = 4, Name = "Garage", Smsname = "garage", Category = "All,", AllowLeave = true, Sequence = 5, CreatedBy = "test", LastModifiedBy = "test" });
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        var summary = await svc.GetSummaryAsync(7, ct);

        Assert.NotNull(summary);
        Assert.Equal(["Garage", "Front porch"], summary.AtlOptions.Select(o => o.Name));
        Assert.Equal([4, 1], summary.AtlOptions.Select(o => o.Id));
    }

    [Fact]
    public async Task GetSummary_excludes_configured_atl_options_by_id_whatever_the_row_is_named()
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc));
        var ct = TestContext.Current.CancellationToken;

        db.TucJobs.Add(new TucJob { UcjbId = 7, UcjbNumber = "JOB-7" });
        db.TblJobLeaveNotHomes.AddRange(
            NewAtlOption(LeaveNotHomeOption.LetterBox, "Mailslot by the gate", 5),
            NewAtlOption(LeaveNotHomeOption.FrontDoor, "Front door", 4),
            NewAtlOption(LeaveNotHomeOption.DropBox, "Drop box", 3));
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        var summary = await svc.GetSummaryAsync(7, ct);

        Assert.NotNull(summary);
        Assert.Equal(
            [(int)LeaveNotHomeOption.FrontDoor, (int)LeaveNotHomeOption.DropBox],
            summary.AtlOptions.Select(o => o.Id));
    }

    [Fact]
    public async Task GetSummary_excludes_every_configured_atl_option()
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc));
        var ct = TestContext.Current.CancellationToken;

        db.TucJobs.Add(new TucJob { UcjbId = 7, UcjbNumber = "JOB-7" });
        db.TblJobLeaveNotHomes.AddRange(
            NewAtlOption(LeaveNotHomeOption.LetterBox, "Letter box", 5),
            NewAtlOption(LeaveNotHomeOption.MailRoom, "Mail room", 4),
            NewAtlOption(LeaveNotHomeOption.FrontDoor, "Front door", 3));
        await db.SaveChangesAsync(ct);

        var opts = DespatchOpts(
            excludedAtlOptions: [LeaveNotHomeOption.LetterBox, LeaveNotHomeOption.MailRoom]);
        var svc = new PaxBookingService(db, opts, NewCache(), time, NewCalendar());

        var summary = await svc.GetSummaryAsync(7, ct);

        Assert.NotNull(summary);
        Assert.Equal([(int)LeaveNotHomeOption.FrontDoor], summary.AtlOptions.Select(o => o.Id));
    }

    [Fact]
    public async Task GetSummary_keeps_every_atl_option_when_nothing_is_excluded()
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc));
        var ct = TestContext.Current.CancellationToken;

        db.TucJobs.Add(new TucJob { UcjbId = 7, UcjbNumber = "JOB-7" });
        db.TblJobLeaveNotHomes.AddRange(
            NewAtlOption(LeaveNotHomeOption.LetterBox, "Letter box", 5),
            NewAtlOption(LeaveNotHomeOption.FrontDoor, "Front door", 4));
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(excludedAtlOptions: []), NewCache(), time,
            NewCalendar());

        var summary = await svc.GetSummaryAsync(7, ct);

        Assert.NotNull(summary);
        Assert.Equal(
            [(int)LeaveNotHomeOption.LetterBox, (int)LeaveNotHomeOption.FrontDoor],
            summary.AtlOptions.Select(o => o.Id));
    }

    [Fact]
    public async Task GetSummary_defaults_the_atl_option_to_the_configured_one()
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc));
        var ct = TestContext.Current.CancellationToken;

        db.TucJobs.Add(new TucJob { UcjbId = 7, UcjbNumber = "JOB-7" });
        db.TblJobLeaveNotHomes.AddRange(
            NewAtlOption(LeaveNotHomeOption.Reception, "Reception", 9),
            NewAtlOption(LeaveNotHomeOption.FrontDoor, "Front door", 4));
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        var summary = await svc.GetSummaryAsync(7, ct);

        Assert.NotNull(summary);
        Assert.Equal((int)LeaveNotHomeOption.FrontDoor, summary.DefaultAtlOptionId);
    }

    [Fact]
    public async Task GetSummary_falls_back_to_the_first_atl_option_when_the_configured_one_is_absent()
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc));
        var ct = TestContext.Current.CancellationToken;

        db.TucJobs.Add(new TucJob { UcjbId = 7, UcjbNumber = "JOB-7" });
        db.TblJobLeaveNotHomes.AddRange(
            NewAtlOption(LeaveNotHomeOption.Reception, "Reception", 9),
            NewAtlOption(LeaveNotHomeOption.Dock, "Dock", 4));
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        var summary = await svc.GetSummaryAsync(7, ct);

        Assert.NotNull(summary);
        Assert.Equal((int)LeaveNotHomeOption.Reception, summary.DefaultAtlOptionId);
    }

    [Fact]
    public async Task GetSummary_has_no_default_atl_option_when_the_tenant_offers_none()
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc));
        var ct = TestContext.Current.CancellationToken;

        db.TucJobs.Add(new TucJob { UcjbId = 7, UcjbNumber = "JOB-7" });
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        var summary = await svc.GetSummaryAsync(7, ct);

        Assert.NotNull(summary);
        Assert.Empty(summary.AtlOptions);
        Assert.Null(summary.DefaultAtlOptionId);
    }

    [Fact]
    public async Task GetSummary_returns_the_stored_extra_delivery_information()
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc));
        var ct = TestContext.Current.CancellationToken;

        db.TucJobs.Add(new TucJob
        {
            UcjbId = 7,
            UcjbNumber = "JOB-7",
            DeliveryAddressLine2 = "Building B, ring the buzzer",
            DeliveryAddressLine4 = "1 Test Street",
            DeliveryAddressLine6 = "Auckland",
            DeliveryAddressLine8 = "NZ"
        });
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        var summary = await svc.GetSummaryAsync(7, ct);

        Assert.NotNull(summary);
        Assert.Equal("Building B, ring the buzzer", summary.DeliveryAddress.Line2);
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
        Assert.Equal("12", addr.Line3);
        Assert.Equal("Queen St", addr.Line4);
        Assert.Equal("CBD", addr.Line5);
        Assert.Equal("Auckland", addr.Line6);
        Assert.Equal("1010", addr.Line7);
        Assert.Equal("NZ", addr.Country);
        Assert.Equal(-36.8485m, addr.Latitude);
        Assert.Equal(174.7633m, addr.Longitude);
    }

    [Fact]
    public async Task GetSummary_returns_the_worldtracer_file_reference_not_the_job_id_or_number()
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc));
        var ct = TestContext.Current.CancellationToken;

        db.TucJobs.Add(new TucJob
        {
            UcjbId = 179252, UcjbNumber = "URG-179252", UcjbClientRefa = " AKLA2633476 "
        });
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        var summary = await svc.GetSummaryAsync(179252, ct);

        Assert.NotNull(summary);
        Assert.Equal("AKLA2633476", summary.FileReference);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetSummary_returns_an_empty_file_reference_when_the_job_has_none(
        string? clientRefa)
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc));
        var ct = TestContext.Current.CancellationToken;

        db.TucJobs.Add(new TucJob { UcjbId = 7, UcjbNumber = "URG-7", UcjbClientRefa = clientRefa });
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        var summary = await svc.GetSummaryAsync(7, ct);

        Assert.NotNull(summary);
        Assert.Equal(string.Empty, summary.FileReference);
    }

    [Fact]
    public async Task GetNotificationDetails_returns_the_trimmed_file_reference_and_the_client_name()
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
        db.TucJobs.Add(new TucJob
        {
            UcjbId = 7, UcjbNumber = "URG-7", UcjbClientId = 3, UcjbClientRefa = " AKLNZ12345 "
        });
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        var details = await svc.GetNotificationDetailsAsync(7, ct);

        Assert.NotNull(details);
        Assert.Equal("AKLNZ12345", details.FileReference);
        Assert.Equal("Air New Zealand", details.AirlineLabel);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetNotificationDetails_returns_an_empty_file_reference_when_the_job_has_none(
        string? clientRefa)
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc));
        var ct = TestContext.Current.CancellationToken;

        db.TucJobs.Add(new TucJob { UcjbId = 7, UcjbNumber = "URG-7", UcjbClientRefa = clientRefa });
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        var details = await svc.GetNotificationDetailsAsync(7, ct);

        Assert.NotNull(details);
        Assert.Equal(string.Empty, details.FileReference);
    }

    [Fact]
    public async Task GetNotificationDetails_falls_back_to_the_generic_airline_when_the_job_has_no_client()
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc));
        var ct = TestContext.Current.CancellationToken;

        db.TucJobs.Add(new TucJob { UcjbId = 7, UcjbNumber = "URG-7" });
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        var details = await svc.GetNotificationDetailsAsync(7, ct);

        Assert.NotNull(details);
        Assert.Equal("Your Airline", details.AirlineLabel);
    }

    [Fact]
    public async Task GetNotificationDetails_returns_null_when_despatch_has_no_such_job()
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc));
        var ct = TestContext.Current.CancellationToken;

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        Assert.Null(await svc.GetNotificationDetailsAsync(404, ct));
    }

    [Fact]
    public async Task GetSummary_extracts_airline_code_from_worldtracer_ref()
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc));
        var ct = TestContext.Current.CancellationToken;

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
        AddJob(db, 7, 77);
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts("0800 111 222"), NewCache(), time,
            NewCalendar());

        var summary = await svc.GetSummaryAsync(7, ct);

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
        AddJob(db, 7, 77);
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
        Assert.Equal(string.Empty, addr.Line4);
        Assert.Equal(string.Empty, addr.Line6);
        Assert.Equal("NZ", addr.Country);
        Assert.Null(addr.Latitude);
        Assert.Null(addr.Longitude);
    }

    [Theory]
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
            DeliveryAddressLine5 = "New York",
            DeliveryAddressLine8 = "United States of America"
        });
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        var summary = await svc.GetSummaryAsync(7, ct);

        Assert.NotNull(summary);
        var addr = summary.DeliveryAddress;
        Assert.Equal("US", addr.Country);
        Assert.Equal("5th Ave", addr.Line4);
        Assert.Equal("New York", addr.Line5);
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
                Line3 = "1",
                Line4 = "Test Street",
                Line5 = "Ponsonby",
                Line6 = "Auckland",
                Line7 = "1011",
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

    [Fact]
    public async Task GetSummary_returns_the_urgent_job_number_alongside_the_file_reference()
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc));
        var ct = TestContext.Current.CancellationToken;

        db.TucJobs.Add(new TucJob
        {
            UcjbId = 179252, UcjbNumber = "URG-179252", UcjbClientRefa = "AKLA2633476"
        });
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        var summary = await svc.GetSummaryAsync(179252, ct);

        Assert.NotNull(summary);
        Assert.Equal("URG-179252", summary.JobNumber);
        Assert.Equal("AKLA2633476", summary.FileReference);
    }

    [Fact]
    public async Task GetSummary_reports_no_confirmation_before_the_passenger_confirms()
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc));
        var ct = TestContext.Current.CancellationToken;

        db.TucJobs.Add(new TucJob { UcjbId = 7, UcjbNumber = "URG-7" });
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        var summary = await svc.GetSummaryAsync(7, ct);

        Assert.NotNull(summary);
        Assert.Null(summary.Confirmation);
    }

    [Fact]
    public async Task GetSummary_reads_the_booked_window_back_after_a_confirmation()
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc));
        var ct = TestContext.Current.CancellationToken;

        db.TucClients.Add(NewClient(77, economyRuns: true, StandardRuns));
        db.TucJobTypes.Add(NewJobType(BaggageSpeed, minutes: 180));
        db.TblJobLeaveNotHomes.Add(NewAtlOption(LeaveNotHomeOption.FrontDoor, "Front door", 10));
        AddJob(db, 4242, 77);
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        await svc.ConfirmAsync(NewConfirmInput(4242,
            deliveryTimeUtc: new DateTime(2026, 6, 10, 21, 0, 0, DateTimeKind.Utc),
            atlOptionId: (int)LeaveNotHomeOption.FrontDoor,
            accessNotes: "Gate code 1234"), ct);

        var summary = await svc.GetSummaryAsync(4242, ct);

        Assert.NotNull(summary);
        var confirmation = summary.Confirmation;
        Assert.NotNull(confirmation);
        Assert.Equal(new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc), confirmation.ConfirmedAtUtc);
        Assert.Equal(new DateTime(2026, 6, 10, 21, 0, 0, DateTimeKind.Utc), confirmation.DeliveryTimeUtc);
        Assert.Equal("Tomorrow, Thu 11 Jun", confirmation.DayLabel);
        Assert.Equal("9:00 AM – 12:00 PM", confirmation.WindowLabel);
        Assert.Equal((int)LeaveNotHomeOption.FrontDoor, confirmation.AtlOptionId);
        Assert.Equal("Gate code 1234", confirmation.AccessNotes);
    }

    [Theory]
    [InlineData(90, "9:00 AM – 10:30 AM")]
    [InlineData(null, "9:00 AM – 12:00 PM")]
    public async Task GetSummary_rebuilds_the_confirmed_window_from_the_job_speed(
        int? speedMinutes, string expectedWindow)
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc));
        var ct = TestContext.Current.CancellationToken;

        db.TucClients.Add(NewClient(77, economyRuns: true, StandardRuns));
        db.TucJobTypes.Add(NewJobType(BaggageSpeed, minutes: speedMinutes));
        AddJob(db, 4242, 77);
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        await svc.ConfirmAsync(NewConfirmInput(4242,
            deliveryTimeUtc: new DateTime(2026, 6, 10, 21, 0, 0, DateTimeKind.Utc)), ct);

        var summary = await svc.GetSummaryAsync(4242, ct);

        Assert.NotNull(summary?.Confirmation);
        Assert.Equal(expectedWindow, summary.Confirmation.WindowLabel);
    }

    [Fact]
    public async Task Confirm_refuses_a_second_confirmation_of_the_same_booking()
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc));
        var ct = TestContext.Current.CancellationToken;

        db.TucJobs.Add(new TucJob { UcjbId = 4242, UcjbNumber = "TEST-4242" });
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        await svc.ConfirmAsync(NewConfirmInput(4242, passengerName: "Jane Pax"), ct);

        await Assert.ThrowsAsync<PaxAlreadyConfirmedException>(() =>
            svc.ConfirmAsync(NewConfirmInput(4242, passengerName: "Someone Else"), ct));

        var job = await db.TucJobs.AsNoTracking().SingleAsync(j => j.UcjbId == 4242, ct);
        Assert.Equal("Jane Pax", job.DeliverToContact);
    }

    [Theory]
    [InlineData(null, (int)JobStatus.Dispatched, false)]
    [InlineData(0, (int)JobStatus.Dispatched, false)]
    [InlineData(88, (int)JobStatus.New, false)]
    [InlineData(88, (int)JobStatus.Dispatched, true)]
    [InlineData(88, (int)JobStatus.Completed, true)]
    [InlineData(88, (int)JobStatus.Void, false)]
    public async Task GetSummary_offers_tracking_only_once_a_courier_is_under_way(
        int? courierId, int status, bool expected)
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc));
        var ct = TestContext.Current.CancellationToken;

        db.TucJobs.Add(new TucJob
        {
            UcjbId = 7, UcjbNumber = "URG-7", UcjbCourierId = courierId, UcjbStatus = status
        });
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        var summary = await svc.GetSummaryAsync(7, ct);

        Assert.NotNull(summary);
        Assert.Equal(expected, summary.TrackingAvailable);
    }

    [Fact]
    public async Task Confirm_writes_every_address_line_straight_through()
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc));
        var ct = TestContext.Current.CancellationToken;

        db.TucJobs.Add(new TucJob { UcjbId = 4242, UcjbNumber = "TEST-4242" });
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        await svc.ConfirmAsync(new ConfirmBookingInput(
            JobId: 4242,
            Address: new AddressUpdateDto
            {
                Line1 = "Sofitel Auckland",
                Line2 = "Room 402",
                Line3 = "21",
                Line4 = "Viaduct Harbour Ave",
                Line5 = "Auckland CBD",
                Line6 = "Auckland",
                Line7 = "1010",
                Country = "NZ"
            },
            DeliveryTimeUtc: new DateTime(2026, 6, 11, 0, 0, 0, DateTimeKind.Utc),
            AtlOptionId: null,
            AccessNotes: null,
            PassengerName: "Jane Pax",
            PassengerPhone: "+64 21 000",
            PassengerEmail: "jane@example.com"), ct);

        var job = await db.TucJobs.AsNoTracking().SingleAsync(j => j.UcjbId == 4242, ct);
        Assert.Equal("Sofitel Auckland", job.DeliveryAddressLine1);
        Assert.Equal("Room 402", job.DeliveryAddressLine2);
        Assert.Equal("21", job.DeliveryAddressLine3);
        Assert.Equal("Viaduct Harbour Ave", job.DeliveryAddressLine4);
        Assert.Equal("Auckland CBD", job.DeliveryAddressLine5);
        Assert.Equal("Auckland", job.DeliveryAddressLine6);
        Assert.Equal("1010", job.DeliveryAddressLine7);
        Assert.Equal("NZ", job.DeliveryAddressLine8);
    }

    [Fact]
    public async Task GetSummary_reads_every_address_line_straight_through()
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc));
        var ct = TestContext.Current.CancellationToken;

        db.TucJobs.Add(new TucJob
        {
            UcjbId = 7,
            UcjbNumber = "URG-7",
            DeliveryAddressLine1 = "Sofitel Auckland",
            DeliveryAddressLine2 = "Room 402",
            DeliveryAddressLine3 = "21",
            DeliveryAddressLine4 = "Viaduct Harbour Ave",
            DeliveryAddressLine5 = "Auckland CBD",
            DeliveryAddressLine6 = "Auckland",
            DeliveryAddressLine7 = "1010",
            DeliveryAddressLine8 = "NZ"
        });
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        var summary = await svc.GetSummaryAsync(7, ct);

        Assert.NotNull(summary);
        var addr = summary.DeliveryAddress;
        Assert.Equal("Sofitel Auckland", addr.Line1);
        Assert.Equal("Room 402", addr.Line2);
        Assert.Equal("21", addr.Line3);
        Assert.Equal("Viaduct Harbour Ave", addr.Line4);
        Assert.Equal("Auckland CBD", addr.Line5);
        Assert.Equal("Auckland", addr.Line6);
        Assert.Equal("1010", addr.Line7);
        Assert.Equal("NZ", addr.Country);
    }

    [Fact]
    public async Task Confirm_gives_a_us_address_no_special_column_layout()
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc));
        var ct = TestContext.Current.CancellationToken;

        db.TucJobs.Add(new TucJob { UcjbId = 4242, UcjbNumber = "TEST-4242" });
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time, NewCalendar());

        await svc.ConfirmAsync(new ConfirmBookingInput(
            JobId: 4242,
            Address: new AddressUpdateDto
            {
                Line3 = "350",
                Line4 = "5th Ave",
                Line5 = "New York",
                Line6 = "NY",
                Line7 = "10118",
                Country = "United States"
            },
            DeliveryTimeUtc: new DateTime(2026, 6, 11, 0, 0, 0, DateTimeKind.Utc),
            AtlOptionId: null,
            AccessNotes: null,
            PassengerName: "Jane Pax",
            PassengerPhone: "+1 555 0100",
            PassengerEmail: "jane@example.com"), ct);

        var job = await db.TucJobs.AsNoTracking().SingleAsync(j => j.UcjbId == 4242, ct);
        Assert.Equal("350", job.DeliveryAddressLine3);
        Assert.Equal("5th Ave", job.DeliveryAddressLine4);
        Assert.Equal("New York", job.DeliveryAddressLine5);
        Assert.Equal("NY", job.DeliveryAddressLine6);
        Assert.Equal("10118", job.DeliveryAddressLine7);
        Assert.Equal("US", job.DeliveryAddressLine8);
    }

    private static ConfirmBookingInput NewConfirmInput(
        int jobId,
        DateTime? deliveryTimeUtc = null,
        int? atlOptionId = null,
        string? accessNotes = null,
        string passengerName = "Jane Pax",
        string? streetNumber = "1",
        string street = "Test Street",
        string city = "Auckland",
        string country = "NZ") =>
        new(
            JobId: jobId,
            Address: new AddressUpdateDto
            {
                Line3 = streetNumber,
                Line4 = street,
                Line5 = "Ponsonby",
                Line6 = city,
                Line7 = "1011",
                Country = country
            },
            DeliveryTimeUtc: deliveryTimeUtc ?? new DateTime(2026, 6, 11, 0, 0, 0, DateTimeKind.Utc),
            AtlOptionId: atlOptionId,
            AccessNotes: accessNotes,
            PassengerName: passengerName,
            PassengerPhone: "+64 21 000",
            PassengerEmail: "jane@example.com");
}
