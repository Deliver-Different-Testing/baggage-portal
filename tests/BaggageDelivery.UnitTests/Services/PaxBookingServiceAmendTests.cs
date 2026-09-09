using BaggageDelivery.Core.Enums;
using BaggageDelivery.Core.Http;
using BaggageDelivery.Core.Interfaces;
using BaggageDelivery.Core.Models;
using BaggageDelivery.Core.Services;
using BaggageDelivery.UnitTests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace BaggageDelivery.UnitTests.Services;

public class PaxBookingServiceAmendTests
{
    private const int JobId = 8080;
    private const int BaggageSpeed = 38;

    private static readonly DateTime NowUtc = new(2026, 9, 9, 3, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime BookedLocal = new(2026, 9, 9, 17, 0, 0);
    private static readonly DateTime BookedUtc = new(2026, 9, 9, 5, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime NextDayUtc = new(2026, 9, 9, 21, 0, 0, DateTimeKind.Utc);

    private static TucJobType NewJobType() => new()
    {
        UcjtId = BaggageSpeed, UcjtName = "Economy Run", SystemName = "ER", Minutes = 180,
        CreatedBy = "test", LastModifiedBy = "test"
    };

    private static TucJob NewJob(
        int? status = (int)JobStatus.Dispatched,
        DateTime? outForDelivery = null) => new()
    {
        UcjbId = JobId,
        UcjbNumber = "TEST-8080",
        UcjbSpeed = BaggageSpeed,
        UcjbStatus = status,
        OutForDelivery = outForDelivery,
        DeliverByTime = BookedLocal,
        UcjbDate = BookedLocal.Date,
        UcjbTime = BookedLocal,
        DeliverToContact = "Jane Pax",
        DeliverToPhone = "+64 21 111 111",
        ProofOfDeliveryEmail = "jane@example.com",
        DeliverToLeaveId = (int)LeaveNotHomeOption.FrontDoor,
        UcjbToSpecial = "Ring the bell",
        DeliveryAddressLine3 = "1",
        DeliveryAddressLine4 = "Test Street",
        DeliveryAddressLine5 = "Ponsonby",
        DeliveryAddressLine6 = "Auckland",
        DeliveryAddressLine7 = "1011",
        DeliveryAddressLine8 = "NZ",
        UcjbToAddr = "1, Test Street, Ponsonby, Auckland, 1011, NZ",
        DeliveryLatitude = -36.85m,
        DeliveryLongitude = 174.76m,
        ScheduleId = 12
    };

    private static JobDeliveryJourney ConfirmedMarker() => new()
    {
        JobId = JobId,
        ChangeType = nameof(DeliveryJourneyChangeType.BaggageDeliveryBooking),
        UpdatedAt = new DateTime(2026, 9, 8, 20, 0, 0, DateTimeKind.Utc),
        UpdatedByType = nameof(DeliveryJourneyUpdatedByType.System),
        Comments = "Baggage delivery booking created by passenger via self-service link"
    };

    private static PaxBookingService NewService(BaggageDeliveryContext db, DateTime? nowUtc = null) =>
        new(
            db,
            Options.Create(new DespatchOptions { TimeZone = "Pacific/Auckland" }),
            Options.Create(new AllowedServiceOptions { Enabled = true }),
            new MemoryCache(new MemoryCacheOptions()),
            new FakeTimeProvider(nowUtc ?? NowUtc),
            Substitute.For<IDespatchCalendar>(),
            Substitute.For<ISuburbResolver>(),
            Substitute.For<IServiceAvailabilityService>(),
            Substitute.For<INotificationService>());

    private static AmendBookingInput Input(
        DateTime? deliveryTimeUtc = null,
        string? accessNotes = "Leave with the concierge") => new(
        JobId: JobId,
        DeliveryTimeUtc: deliveryTimeUtc ?? NextDayUtc,
        AtlOptionId: (int)LeaveNotHomeOption.Reception,
        AccessNotes: accessNotes,
        PassengerName: "Jane Passenger",
        PassengerPhone: "+64 21 222 222",
        PassengerEmail: "jane.new@example.com");

    private static TblJobLeaveNotHome NewAtlOption(LeaveNotHomeOption id) => new()
    {
        LeaveNotHomeId = (int)id, Name = id.ToString(), Smsname = id.ToString(), Category = "All,",
        AllowLeave = true, Sequence = 1, CreatedBy = "test", LastModifiedBy = "test"
    };

    private static async Task SeedAsync(BaggageDeliveryContext db, TucJob job, bool confirmed = true)
    {
        db.TucJobTypes.Add(NewJobType());
        db.TblJobLeaveNotHomes.Add(NewAtlOption(LeaveNotHomeOption.FrontDoor));
        db.TblJobLeaveNotHomes.Add(NewAtlOption(LeaveNotHomeOption.Reception));
        db.TucJobs.Add(job);
        if (confirmed)
        {
            db.JobDeliveryJourneys.Add(ConfirmedMarker());
        }

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Amending_inside_the_change_window_moves_the_delivery_and_contact_details()
    {
        await using var db = InMemoryDb.NewContext();
        await SeedAsync(db, NewJob());
        var ct = TestContext.Current.CancellationToken;

        await NewService(db).AmendAsync(Input(), ct);

        var job = await db.TucJobs.AsNoTracking().SingleAsync(j => j.UcjbId == JobId, ct);
        Assert.Equal(new DateTime(2026, 9, 10, 9, 0, 0), job.DeliverByTime);
        Assert.Equal(new DateTime(2026, 9, 10), job.UcjbDate);
        Assert.Equal(new DateTime(2026, 9, 10, 9, 0, 0), job.UcjbTime);
        Assert.Equal("Jane Passenger", job.DeliverToContact);
        Assert.Equal("+64 21 222 222", job.DeliverToPhone);
        Assert.Equal("jane.new@example.com", job.ProofOfDeliveryEmail);
        Assert.Equal((int)LeaveNotHomeOption.Reception, job.DeliverToLeaveId);
        Assert.Equal("Ring the bell", job.UcjbToSpecial);
    }

    [Fact]
    public async Task Amending_appends_the_additional_details_as_a_new_delivery_note()
    {
        await using var db = InMemoryDb.NewContext();
        await SeedAsync(db, NewJob());
        var ct = TestContext.Current.CancellationToken;

        await NewService(db).AmendAsync(Input(), ct);

        var note = await db.TucNotes.AsNoTracking().SingleAsync(n => n.JobId == JobId, ct);
        Assert.Equal((int)NoteType.DeliveryNotes, note.NoteTypeId);
        Assert.Equal("Leave with the concierge", note.NoteText);
        Assert.False(note.IsImportant);
        Assert.Null(note.CreatedBy);
        Assert.Equal(NowUtc.AddHours(12), note.CreatedDate);
        Assert.Equal(NowUtc, note.CreatedDateUtc);
    }

    [Fact]
    public async Task Amending_without_additional_details_creates_no_delivery_note()
    {
        await using var db = InMemoryDb.NewContext();
        await SeedAsync(db, NewJob());
        var ct = TestContext.Current.CancellationToken;

        await NewService(db).AmendAsync(Input(accessNotes: "  "), ct);

        Assert.False(await db.TucNotes.AsNoTracking().AnyAsync(ct));
    }

    [Fact]
    public async Task Amending_twice_keeps_both_delivery_notes_and_serves_the_newest()
    {
        await using var db = InMemoryDb.NewContext();
        await SeedAsync(db, NewJob());
        var ct = TestContext.Current.CancellationToken;
        var svc = NewService(db);

        await svc.AmendAsync(Input(), ct);
        await svc.AmendAsync(Input(accessNotes: "Side gate is unlocked"), ct);

        var notes = await db.TucNotes.AsNoTracking()
            .Where(n => n.JobId == JobId).OrderBy(n => n.NoteId)
            .Select(n => n.NoteText).ToListAsync(ct);
        Assert.Equal(new[] { "Leave with the concierge", "Side gate is unlocked" }, notes);

        var summary = await svc.GetSummaryAsync(JobId, ct);
        Assert.NotNull(summary);
        Assert.NotNull(summary.Confirmation);
        Assert.Equal("Side gate is unlocked", summary.Confirmation.AccessNotes);
    }

    [Fact]
    public async Task Amending_leaves_the_delivery_address_and_service_untouched()
    {
        await using var db = InMemoryDb.NewContext();
        await SeedAsync(db, NewJob());
        var ct = TestContext.Current.CancellationToken;

        await NewService(db).AmendAsync(Input(), ct);

        var job = await db.TucJobs.AsNoTracking().SingleAsync(j => j.UcjbId == JobId, ct);
        Assert.Equal("Test Street", job.DeliveryAddressLine4);
        Assert.Equal("Ponsonby", job.DeliveryAddressLine5);
        Assert.Equal("1, Test Street, Ponsonby, Auckland, 1011, NZ", job.UcjbToAddr);
        Assert.Equal(-36.85m, job.DeliveryLatitude);
        Assert.Equal(174.76m, job.DeliveryLongitude);
        Assert.Equal(BaggageSpeed, job.UcjbSpeed);
        Assert.Equal(12, job.ScheduleId);
        Assert.Equal((int)JobStatus.Dispatched, job.UcjbStatus);
    }

    [Fact]
    public async Task Amending_records_a_delivery_journey_entry_with_the_old_and_new_window()
    {
        await using var db = InMemoryDb.NewContext();
        await SeedAsync(db, NewJob());
        var ct = TestContext.Current.CancellationToken;

        await NewService(db).AmendAsync(Input(), ct);

        var journey = await db.JobDeliveryJourneys.AsNoTracking()
            .Where(j => j.JobId == JobId)
            .OrderByDescending(j => j.JourneyId)
            .FirstAsync(ct);

        Assert.Equal(nameof(DeliveryJourneyChangeType.BaggageDeliveryBooking), journey.ChangeType);
        Assert.Equal("DeliverByTime", journey.FieldName);
        Assert.Equal("Wed 9 Sep 2026, 5:00 PM – 8:00 PM", journey.OldValue);
        Assert.Equal("Thu 10 Sep 2026, 9:00 AM – 12:00 PM", journey.NewValue);
        Assert.Equal(nameof(DeliveryJourneyUpdatedByType.System), journey.UpdatedByType);
        Assert.Contains("updated by the passenger", journey.Comments);
    }

    [Fact]
    public async Task Amending_after_the_cutoff_is_refused()
    {
        await using var db = InMemoryDb.NewContext();
        await SeedAsync(db, NewJob());
        var ct = TestContext.Current.CancellationToken;

        var svc = NewService(db, nowUtc: BookedUtc.AddMinutes(-15));

        await Assert.ThrowsAsync<PaxAmendWindowClosedException>(() => svc.AmendAsync(Input(), ct));
    }

    [Fact]
    public async Task Amending_exactly_on_the_cutoff_is_refused()
    {
        await using var db = InMemoryDb.NewContext();
        await SeedAsync(db, NewJob());
        var ct = TestContext.Current.CancellationToken;

        var svc = NewService(db, nowUtc: BookedUtc.AddMinutes(-30));

        await Assert.ThrowsAsync<PaxAmendWindowClosedException>(() => svc.AmendAsync(Input(), ct));
    }

    [Fact]
    public async Task Amending_onto_a_run_inside_the_lead_time_is_refused()
    {
        await using var db = InMemoryDb.NewContext();
        await SeedAsync(db, NewJob());
        var ct = TestContext.Current.CancellationToken;

        var svc = NewService(db);

        await Assert.ThrowsAsync<PaxAmendWindowClosedException>(
            () => svc.AmendAsync(Input(NowUtc.AddMinutes(10)), ct));
    }

    [Fact]
    public async Task Amending_a_booking_that_was_never_confirmed_is_refused()
    {
        await using var db = InMemoryDb.NewContext();
        await SeedAsync(db, NewJob(), confirmed: false);
        var ct = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<PaxNotConfirmedException>(
            () => NewService(db).AmendAsync(Input(), ct));
    }

    [Fact]
    public async Task Amending_a_job_that_is_out_for_delivery_is_refused()
    {
        await using var db = InMemoryDb.NewContext();
        await SeedAsync(db, NewJob(outForDelivery: new DateTime(2026, 9, 9, 14, 30, 0)));
        var ct = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<PaxAmendWindowClosedException>(
            () => NewService(db).AmendAsync(Input(), ct));
    }

    [Theory]
    [InlineData((int)JobStatus.Completed)]
    [InlineData((int)JobStatus.Void)]
    public async Task Amending_a_finished_job_is_refused(int status)
    {
        await using var db = InMemoryDb.NewContext();
        await SeedAsync(db, NewJob(status: status));
        var ct = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<PaxAmendWindowClosedException>(
            () => NewService(db).AmendAsync(Input(), ct));
    }

    [Fact]
    public async Task Amending_an_unknown_job_is_refused()
    {
        await using var db = InMemoryDb.NewContext();
        await SeedAsync(db, NewJob());
        var ct = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<PaxNotConfirmedException>(
            () => NewService(db).AmendAsync(Input() with { JobId = 9999 }, ct));
    }

    [Fact]
    public async Task A_confirmed_booking_is_editable_until_the_lead_time_before_the_run()
    {
        await using var db = InMemoryDb.NewContext();
        await SeedAsync(db, NewJob());
        var ct = TestContext.Current.CancellationToken;

        var summary = await NewService(db).GetSummaryAsync(JobId, ct);

        Assert.NotNull(summary?.Confirmation);
        Assert.True(summary.Confirmation.CanEdit);
        Assert.Equal(BookedUtc.AddMinutes(-30), summary.Confirmation.EditableUntilUtc);
        Assert.Equal(30, summary.BookingLeadTimeMinutes);
    }

    [Fact]
    public async Task A_confirmed_booking_is_no_longer_editable_past_the_cutoff()
    {
        await using var db = InMemoryDb.NewContext();
        await SeedAsync(db, NewJob());
        var ct = TestContext.Current.CancellationToken;

        var summary = await NewService(db, nowUtc: BookedUtc.AddMinutes(-15))
            .GetSummaryAsync(JobId, ct);

        Assert.False(summary?.Confirmation?.CanEdit);
    }

    [Fact]
    public async Task A_booking_already_out_for_delivery_is_not_editable()
    {
        await using var db = InMemoryDb.NewContext();
        await SeedAsync(db, NewJob(outForDelivery: new DateTime(2026, 9, 9, 14, 30, 0)));
        var ct = TestContext.Current.CancellationToken;

        var summary = await NewService(db).GetSummaryAsync(JobId, ct);

        Assert.False(summary?.Confirmation?.CanEdit);
    }
}
