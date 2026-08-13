using BaggageDelivery.Core.Enums;
using BaggageDelivery.Core.Http;
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
    private static IOptions<DespatchOptions> DespatchOpts() =>
        Options.Create(new DespatchOptions { TimeZone = "Pacific/Auckland" });

    // Fresh cache per service so reference-data caching can't leak between tests.
    private static MemoryCache NewCache() => new(new MemoryCacheOptions());

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
            EconomyRun5 = new DateTime(1900, 1, 1, 19, 0, 0),
            EconomyCutOff = new DateTime(1900, 1, 1, 21, 0, 0)
        });
        await db.SaveChangesAsync(CancellationToken.None);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time);

        var slots = await svc.GetTimeslotsAsync(localDate: new DateTime(2026, 6, 10),
            CancellationToken.None);

        Assert.Equal(5, slots.Count);
        Assert.Equal("9:00 AM - 12:00 PM", slots[0].Label);
        Assert.Equal("12:00 PM - 3:00 PM", slots[1].Label);
        Assert.Equal("3:00 PM - 5:00 PM", slots[2].Label);
        Assert.Equal("5:00 PM - 7:00 PM", slots[3].Label);
        Assert.Equal("7:00 PM - 9:00 PM", slots[4].Label);
    }

    [Fact]
    public async Task GetTimeslots_omits_final_window_when_cutoff_is_null()
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
            EconomyRun5 = new DateTime(1900, 1, 1, 19, 0, 0),
            EconomyCutOff = null
        });
        await db.SaveChangesAsync(CancellationToken.None);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time);

        var slots = await svc.GetTimeslotsAsync(localDate: new DateTime(2026, 6, 10),
            CancellationToken.None);

        Assert.Equal(4, slots.Count);
        Assert.Equal("5:00 PM - 7:00 PM", slots[3].Label);
    }

    [Theory]
    [InlineData(18)]
    [InlineData(19)]
    public async Task GetTimeslots_omits_final_window_when_cutoff_is_not_after_last_run(int cutOffHour)
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
            EconomyRun5 = new DateTime(1900, 1, 1, 19, 0, 0),
            EconomyCutOff = new DateTime(1900, 1, 1, cutOffHour, 0, 0)
        });
        await db.SaveChangesAsync(CancellationToken.None);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time);

        var slots = await svc.GetTimeslotsAsync(localDate: new DateTime(2026, 6, 10),
            CancellationToken.None);

        Assert.Equal(4, slots.Count);
        Assert.Equal("5:00 PM - 7:00 PM", slots[3].Label);
        Assert.DoesNotContain(slots, s => s.EndUtc <= s.StartUtc);
    }

    [Fact]
    public async Task GetTimeslots_marks_final_window_first_available_after_the_last_run()
    {
        await using var db = InMemoryDb.NewContext();
        // 2026-06-10 08:00 UTC = 2026-06-10 20:00 Pacific/Auckland (NZST UTC+12),
        // i.e. after Run5 (19:00) but before the 21:00 cutoff.
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 8, 0, 0, DateTimeKind.Utc));

        db.TblEcoSettings.Add(new TblEcoSetting
        {
            SettingId = 1,
            EconomyRun1 = new DateTime(1900, 1, 1, 9, 0, 0),
            EconomyRun2 = new DateTime(1900, 1, 1, 12, 0, 0),
            EconomyRun3 = new DateTime(1900, 1, 1, 15, 0, 0),
            EconomyRun4 = new DateTime(1900, 1, 1, 17, 0, 0),
            EconomyRun5 = new DateTime(1900, 1, 1, 19, 0, 0),
            EconomyCutOff = new DateTime(1900, 1, 1, 21, 0, 0)
        });
        await db.SaveChangesAsync(CancellationToken.None);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time);

        var slots = await svc.GetTimeslotsAsync(localDate: new DateTime(2026, 6, 10),
            CancellationToken.None);

        Assert.Equal(5, slots.Count);
        Assert.True(slots[4].FirstAvailable);
        Assert.DoesNotContain(slots.Take(4), s => s.FirstAvailable);
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

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time);

        var slots = await svc.GetTimeslotsAsync(localDate: new DateTime(2026, 6, 10),
            CancellationToken.None);

        Assert.Single(slots);
        Assert.Equal("8:00 AM - 11:00 AM", slots[0].Label);
    }

    [Fact]
    public async Task GetTimeslots_caches_eco_runs_across_calls()
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc));

        db.TblEcoSettings.Add(new TblEcoSetting
        {
            SettingId = 1,
            EconomyRun1 = new DateTime(1900, 1, 1, 9, 0, 0),
            EconomyRun2 = new DateTime(1900, 1, 1, 12, 0, 0)
        });
        await db.SaveChangesAsync(CancellationToken.None);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time);

        var first = await svc.GetTimeslotsAsync(new DateTime(2026, 6, 10), CancellationToken.None);
        Assert.Single(first);

        // Wipe the backing table — a cached read must not hit the DB again.
        await db.TblEcoSettings.ExecuteDeleteAsync(CancellationToken.None);

        var second = await svc.GetTimeslotsAsync(new DateTime(2026, 6, 10), CancellationToken.None);
        Assert.Single(second);
        Assert.Equal("9:00 AM - 12:00 PM", second[0].Label);
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

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time);

        var slots = await svc.GetTimeslotsAsync(localDate: new DateTime(2026, 6, 10),
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

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time);

        var slots = await svc.GetTimeslotsAsync(localDate: new DateTime(2026, 6, 10),
            CancellationToken.None);

        Assert.Empty(slots);
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

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time);

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
            TimeSlotStartUtc: new DateTime(2026, 6, 10, 21, 0, 0, DateTimeKind.Utc),
            TimeSlotEndUtc: new DateTime(2026, 6, 11, 0, 0, 0, DateTimeKind.Utc),
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

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time);

        await svc.ConfirmAsync(new ConfirmBookingInput(
            JobId: 4243,
            Address: new AddressUpdateDto { Line1 = "1 Test Street", City = "Auckland", Country = "NZ" },
            TimeSlotStartUtc: new DateTime(2026, 6, 10, 21, 0, 0, DateTimeKind.Utc),
            TimeSlotEndUtc: new DateTime(2026, 6, 11, 0, 0, 0, DateTimeKind.Utc),
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
        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time);
        var ct = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ConfirmAsync(new ConfirmBookingInput(
            JobId: 9999,
            Address: new AddressUpdateDto { Line1 = "x", City = "y", Country = "NZ" },
            TimeSlotStartUtc: new DateTime(2026, 6, 10, 21, 0, 0, DateTimeKind.Utc),
            TimeSlotEndUtc: new DateTime(2026, 6, 11, 0, 0, 0, DateTimeKind.Utc),
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

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time);

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

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time);

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
    public async Task GetSummary_extracts_airline_code_from_worldtracer_ref()
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc));
        var ct = TestContext.Current.CancellationToken;

        // WorldTracer file ref: station(3) + airline(2) + sequence -> "NZ".
        db.TucJobs.Add(new TucJob { UcjbId = 7, UcjbNumber = "JOB-7", UcjbClientRefa = "AKLNZ12345" });
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time);

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

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time);

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

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time);

        var summary = await svc.GetSummaryAsync(7, ct);

        Assert.NotNull(summary);
        Assert.Equal("QF", summary.AirlineCode);
    }

    [Fact]
    public async Task GetSummary_surfaces_airline_code_from_job_client_code()
    {
        await using var db = InMemoryDb.NewContext();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc));
        var ct = TestContext.Current.CancellationToken;

        db.TucJobs.Add(new TucJob { UcjbId = 7, UcjbNumber = "JOB-7", UcjbClientCode = "QF" });
        await db.SaveChangesAsync(ct);

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time);

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

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time);

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

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time);

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

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time);

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

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time);

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

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time);

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

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time);

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
            TimeSlotStartUtc: new DateTime(2026, 6, 10, 21, 0, 0, DateTimeKind.Utc),
            TimeSlotEndUtc: new DateTime(2026, 6, 11, 0, 0, 0, DateTimeKind.Utc),
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

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time);

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
            TimeSlotStartUtc: new DateTime(2026, 6, 10, 21, 0, 0, DateTimeKind.Utc),
            TimeSlotEndUtc: new DateTime(2026, 6, 11, 0, 0, 0, DateTimeKind.Utc),
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

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time);

        var summary = await svc.GetSummaryAsync(4242, ct);
        Assert.NotNull(summary);

        // Exactly what the pax portal posts back when the passenger only picks a
        // timeslot: the address object it was handed, untouched.
        await svc.ConfirmAsync(new ConfirmBookingInput(
            JobId: 4242,
            Address: summary.DeliveryAddress,
            TimeSlotStartUtc: new DateTime(2026, 6, 10, 21, 0, 0, DateTimeKind.Utc),
            TimeSlotEndUtc: new DateTime(2026, 6, 11, 0, 0, 0, DateTimeKind.Utc),
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

        var svc = new PaxBookingService(db, DespatchOpts(), NewCache(), time);

        var slots = await svc.GetTimeslotsAsync(localDate: new DateTime(2026, 6, 10),
            CancellationToken.None);

        Assert.False(slots[0].FirstAvailable);
        Assert.False(slots[1].FirstAvailable);
        Assert.True(slots[2].FirstAvailable);
    }
}
