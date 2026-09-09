using BaggageDelivery.Core.Enums;
using BaggageDelivery.Core.Http;
using BaggageDelivery.Core.Http.Models;
using BaggageDelivery.Core.Interfaces;
using BaggageDelivery.Core.Models;
using BaggageDelivery.Core.Notifications;
using BaggageDelivery.Core.Services;
using BaggageDelivery.UnitTests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace BaggageDelivery.UnitTests.Services;

public class PaxBookingServiceGuardRailTests
{
    private const int JobId = 5150;
    private static readonly DateTime NowUtc = new(2026, 9, 9, 3, 0, 0, DateTimeKind.Utc);

    private static CandidateService EconomyRun(int jobTypeId = 37, int? scheduleId = null) =>
        new(jobTypeId, scheduleId, "Economy Run", "ER", null, AvailabilityVerdict.Available, null, 180);

    private static AddressUpdateDto Address(string suburb = "Ponsonby", string postCode = "1011") =>
        new()
        {
            Line3 = "1", Line4 = "Test Street", Line5 = suburb,
            Line6 = "Auckland", Line7 = postCode, Country = "NZ"
        };

    private static TucJobType NewJobType(int speedId) => new()
    {
        UcjtId = speedId, UcjtName = "Economy Run", ShortName = "ER", UcjtDescription = "Economy Run",
        UcjtCode = "ER", JobLetter = "E", SystemName = "ER", Minutes = 180,
        CreatedBy = "test", LastModifiedBy = "test", Notes = "", ExtraName = "", Alias = ""
    };

    private static TucJob NewJob() => new()
    {
        UcjbId = JobId,
        UcjbNumber = "TEST-5150",
        DeliveryAddressLine3 = "1",
        DeliveryAddressLine4 = "Test Street",
        DeliveryAddressLine5 = "Ponsonby",
        DeliveryAddressLine6 = "Auckland",
        DeliveryAddressLine7 = "1011",
        DeliveryAddressLine8 = "NZ",
        UcjbToAddr = "1, Test Street, Ponsonby, Auckland, 1011, NZ",
        UcjbStatus = (int)JobStatus.Dispatched
    };

    private static (PaxBookingService Svc, IServiceAvailabilityService Availability, INotificationService Notify)
        NewService(BaggageDeliveryContext db, params CandidateService[] allowed) =>
        NewService(db, "ops@airnz.co.nz", allowed);

    private static (PaxBookingService Svc, IServiceAvailabilityService Availability, INotificationService Notify)
        NewService(BaggageDeliveryContext db, string notifyEmail, params CandidateService[] allowed)
    {
        var availability = Substitute.For<IServiceAvailabilityService>();
        availability.GetAllowedServicesAsync(
                Arg.Any<int>(), Arg.Any<AddressUpdateDto>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(allowed);

        var notify = Substitute.For<INotificationService>();
        var suburbs = Substitute.For<ISuburbResolver>();
        suburbs.ResolveAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns((int?)null);
        var calendar = Substitute.For<IDespatchCalendar>();

        var svc = new PaxBookingService(
            db,
            Options.Create(new DespatchOptions { TimeZone = "Pacific/Auckland" }),
            Options.Create(new AllowedServiceOptions
            {
                Enabled = true,
                UnserviceableAddressNotifyEmail = notifyEmail
            }),
            new MemoryCache(new MemoryCacheOptions()),
            new FakeTimeProvider(NowUtc),
            calendar,
            suburbs,
            availability,
            notify);

        return (svc, availability, notify);
    }

    private static ConfirmBookingInput Input(AddressUpdateDto address, int? serviceJobTypeId) =>
        new(
            JobId: JobId,
            Address: address,
            DeliveryTimeUtc: new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc),
            AtlOptionId: null,
            AccessNotes: null,
            PassengerName: "Jane Pax",
            PassengerPhone: "+64 21 000",
            PassengerEmail: "jane@example.com",
            ServiceJobTypeId: serviceJobTypeId);

    [Fact]
    public async Task An_unchanged_address_confirms_without_a_service_and_never_checks_availability()
    {
        await using var db = InMemoryDb.NewContext();
        db.TucJobTypes.Add(NewJobType(37));
        db.TucJobs.Add(NewJob());
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var (svc, availability, _) = NewService(db);

        await svc.ConfirmAsync(Input(Address(), serviceJobTypeId: null), TestContext.Current.CancellationToken);

        await availability.DidNotReceiveWithAnyArgs().GetAllowedServicesAsync(
            0, null!, default, TestContext.Current.CancellationToken);
        var job = await db.TucJobs.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal((int)JobStatus.New, job.UcjbStatus);
    }

    [Fact]
    public async Task A_changed_address_without_a_chosen_service_is_refused()
    {
        await using var db = InMemoryDb.NewContext();
        db.TucJobTypes.Add(NewJobType(37));
        db.TucJobs.Add(NewJob());
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var (svc, _, _) = NewService(db, EconomyRun());

        await Assert.ThrowsAsync<PaxServiceNotAllowedException>(() => svc.ConfirmAsync(
            Input(Address(suburb: "Haast", postCode: "7886"), serviceJobTypeId: null),
            TestContext.Current.CancellationToken));

        var job = await db.TucJobs.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal((int)JobStatus.Dispatched, job.UcjbStatus);
        Assert.Equal("Ponsonby", job.DeliveryAddressLine5);
    }

    [Fact]
    public async Task A_changed_address_with_a_service_the_new_address_does_not_support_is_refused()
    {
        await using var db = InMemoryDb.NewContext();
        db.TucJobTypes.Add(NewJobType(37));
        db.TucJobs.Add(NewJob());
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var (svc, _, _) = NewService(db, EconomyRun(jobTypeId: 37));

        await Assert.ThrowsAsync<PaxServiceNotAllowedException>(() => svc.ConfirmAsync(
            Input(Address(suburb: "Haast", postCode: "7886"), serviceJobTypeId: 10),
            TestContext.Current.CancellationToken));

        var job = await db.TucJobs.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal((int)JobStatus.Dispatched, job.UcjbStatus);
    }

    [Fact]
    public async Task A_changed_address_with_an_allowed_service_writes_the_speed_and_the_address()
    {
        await using var db = InMemoryDb.NewContext();
        db.TucJobTypes.Add(NewJobType(37));
        db.TucJobs.Add(NewJob());
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var (svc, _, _) = NewService(db, EconomyRun(jobTypeId: 37));

        await svc.ConfirmAsync(
            Input(Address(suburb: "Haast", postCode: "7886"), serviceJobTypeId: 37),
            TestContext.Current.CancellationToken);

        var job = await db.TucJobs.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(37, job.UcjbSpeed);
        Assert.Null(job.ScheduleId);
        Assert.Equal("Haast", job.DeliveryAddressLine5);
        Assert.Equal((int)JobStatus.New, job.UcjbStatus);
    }

    [Fact]
    public async Task A_scheduled_service_writes_the_underlying_speed_and_the_schedule()
    {
        await using var db = InMemoryDb.NewContext();
        db.TucJobTypes.Add(NewJobType(37));
        db.TucJobs.Add(NewJob());
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var scheduled = new CandidateService(
            4037, 4, "Chch PM run", "ER", null, AvailabilityVerdict.Available, NowUtc, 120);
        var (svc, _, _) = NewService(db, scheduled);

        await svc.ConfirmAsync(
            Input(Address(suburb: "Riccarton", postCode: "8041"), serviceJobTypeId: 4037),
            TestContext.Current.CancellationToken);

        var job = await db.TucJobs.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(37, job.UcjbSpeed);
        Assert.Equal(4, job.ScheduleId);
        Assert.Equal("Chch PM run", job.ScheduleName);
    }

    [Fact]
    public async Task GetAvailableServices_delegates_to_the_availability_service()
    {
        await using var db = InMemoryDb.NewContext();
        var (svc, _, _) = NewService(db, EconomyRun());

        var result = await svc.GetAvailableServicesAsync(
            JobId, Address(), TestContext.Current.CancellationToken);

        Assert.Equal(["Economy Run"], result.Select(s => s.Name));
    }

    [Fact]
    public async Task RequestAirlineContact_notifies_the_airline_and_leaves_the_job_alone()
    {
        await using var db = InMemoryDb.NewContext();
        db.TucJobTypes.Add(NewJobType(37));
        db.TucJobs.Add(NewJob());
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var (svc, _, notify) = NewService(db);

        var sent = await svc.RequestAirlineContactAsync(
            new AddressContactRequestInput(
                JobId, Address(suburb: "Haast", postCode: "7886"),
                "Jane Pax", "+64 21 000", "jane@example.com"),
            TestContext.Current.CancellationToken);

        Assert.True(sent);
        await notify.Received(1).SendAddressUnserviceableAsync(
            JobId,
            Arg.Any<string>(),
            Arg.Is<AddressUnserviceableNotificationContext>(c =>
                c.PassengerName == "Jane Pax"
                && c.RequestedAddress.Contains("Haast")
                && c.CurrentAddress.Contains("Ponsonby")),
            Arg.Any<CancellationToken>());

        var job = await db.TucJobs.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal((int)JobStatus.Dispatched, job.UcjbStatus);
        Assert.Equal("Ponsonby", job.DeliveryAddressLine5);
    }

    [Fact]
    public async Task RequestAirlineContact_returns_false_for_an_unknown_job()
    {
        await using var db = InMemoryDb.NewContext();
        var (svc, _, notify) = NewService(db);

        var sent = await svc.RequestAirlineContactAsync(
            new AddressContactRequestInput(9999, Address(), "Jane Pax", null, null),
            TestContext.Current.CancellationToken);

        Assert.False(sent);
        await notify.DidNotReceiveWithAnyArgs().SendAddressUnserviceableAsync(
            0, null!, null!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RequestAirlineContact_fails_loudly_when_no_airline_address_is_configured()
    {
        await using var db = InMemoryDb.NewContext();
        db.TucJobTypes.Add(NewJobType(37));
        db.TucJobs.Add(NewJob());
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var (svc, _, notify) = NewService(db, notifyEmail: "");

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.RequestAirlineContactAsync(
            new AddressContactRequestInput(JobId, Address(), "Jane Pax", null, null),
            TestContext.Current.CancellationToken));

        await notify.DidNotReceiveWithAnyArgs().SendAddressUnserviceableAsync(
            0, null!, null!, TestContext.Current.CancellationToken);
    }
}
