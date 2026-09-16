using BaggageDelivery.Core.Http;
using BaggageDelivery.Core.Http.Models;
using BaggageDelivery.Core.Interfaces;
using BaggageDelivery.Core.Models;
using BaggageDelivery.Core.Services;
using BaggageDelivery.UnitTests.Helpers;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace BaggageDelivery.UnitTests.Services;

public class ServiceAvailabilityServiceTests
{
    private const int JobId = 4242;
    private const int ClientId = 77;
    private const int StandardSpeed = 37;
    private const int PickupSuburbId = 1;
    private const int CarSizeId = 2;

    // 8pm UTC on 9 Sep 2026 is 8am on the 10th in Auckland
    private static readonly DateTime NowUtc = new(2026, 9, 9, 20, 0, 0, DateTimeKind.Utc);

    private static BagDel_stpAvailableServicesResult Row(
        int? jobTypeId = StandardSpeed,
        string? name = "Standard",
        string? speed = "STD",
        string? description = "Delivered on our next run",
        string? availability = AvailabilityVerdict.Available,
        string? colour = "#00FF00",
        DateTime? bookDate = null,
        int? duration = 180) => new()
    {
        JobTypeID = jobTypeId,
        Name = name,
        Speed = speed,
        Description = description,
        Availability = availability,
        AvailabilityColour = colour,
        BookDate = bookDate,
        Duration = duration
    };

    private static AddressUpdateDto Address(
        string suburb = "Ponsonby", string postCode = "1011",
        decimal? latitude = null, decimal? longitude = null) =>
        new()
        {
            Line3 = "1", Line4 = "Test Street", Line5 = suburb,
            Line6 = "Auckland", Line7 = postCode, Country = "NZ",
            Latitude = latitude, Longitude = longitude
        };

    private static async Task<BaggageDeliveryContext> SeededAsync(bool economyRuns = true)
    {
        var db = InMemoryDb.NewContext();
        db.TucSuburbs.Add(new TucSuburb
        {
            UcsuId = PickupSuburbId, UcsuName = "Airport", UcsuArea = 1, UcsuBaseRegion = 1,
            Smsname = "Airport", PostCode = "2022", CreatedBy = "test", LastModifiedBy = "test"
        });
        db.TblJobSizeNames.Add(new TblJobSizeName
        {
            SizeId = "2", SizeName = "Car", Name = "Car"
        });
        db.TucClients.Add(new TucClient
        {
            UcclId = ClientId, UcclName = "Test Air", UcclLegalName = "Test Air Ltd",
            UcclCode = "TAIR", Smsname = "TestAir", SiteId = 1,
            EconomyActive = true, EconomyRuns = economyRuns,
            CreatedBy = "test", LastModifiedBy = "test"
        });
        db.TucJobs.Add(new TucJob
        {
            UcjbId = JobId,
            UcjbNumber = "TEST-4242",
            UcjbClientId = ClientId,
            UcjbSize = CarSizeId,
            UcjbFrom = PickupSuburbId
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return db;
    }

    private static (ServiceAvailabilityService Svc, IAvailableServicesQuery Query) NewService(
        BaggageDeliveryContext db, params BagDel_stpAvailableServicesResult[] rows)
    {
        var query = Substitute.For<IAvailableServicesQuery>();
        query.ExecuteAsync(Arg.Any<AvailableServicesRequest>(), Arg.Any<CancellationToken>())
            .Returns(rows);

        var svc = new ServiceAvailabilityService(
            db,
            query,
            Options.Create(new AllowedServiceOptions()),
            Options.Create(new DespatchOptions { TimeZone = "Pacific/Auckland" }));

        return (svc, query);
    }

    private static Task<IReadOnlyList<CandidateService>> Get(
        ServiceAvailabilityService svc, AddressUpdateDto? address = null) =>
        svc.GetAllowedServicesAsync(
            JobId, address ?? Address(), NowUtc, TestContext.Current.CancellationToken);

    [Fact]
    public async Task An_allowed_green_row_is_offered()
    {
        await using var db = await SeededAsync();
        var (svc, _) = NewService(db, Row());

        var service = Assert.Single(await Get(svc));

        Assert.Equal(StandardSpeed, service.JobTypeId);
        Assert.Equal("Standard", service.Name);
        Assert.Equal("STD", service.SystemName);
        Assert.Equal("Delivered on our next run", service.Description);
        Assert.Equal(180, service.DurationMinutes);
        Assert.False(service.IsScheduled);
    }

    [Fact]
    public async Task An_amber_row_the_proc_called_Available_is_dropped()
    {
        await using var db = await SeededAsync();
        var (svc, _) = NewService(db, Row(colour: "orange"));

        Assert.Empty(await Get(svc));
    }

    [Fact]
    public async Task A_nationwide_row_with_no_colour_is_offered()
    {
        await using var db = await SeededAsync();
        var (svc, _) = NewService(db,
            Row(jobTypeId: 56, name: "Standard", speed: null, colour: null, duration: 4320));

        Assert.Equal(56, Assert.Single(await Get(svc)).JobTypeId);
    }

    [Fact]
    public async Task A_speed_that_is_not_allow_listed_is_dropped()
    {
        await using var db = await SeededAsync();
        var (svc, _) = NewService(db, Row(jobTypeId: 8, name: "Two Hour", speed: "TH"));

        Assert.Empty(await Get(svc));
    }

    [Fact]
    public async Task A_composite_id_is_split_into_schedule_and_speed()
    {
        await using var db = await SeededAsync();
        var (svc, _) = NewService(db, Row(jobTypeId: 4037, name: "Standard"));

        var service = Assert.Single(await Get(svc));

        Assert.True(service.IsScheduled);
        Assert.Equal(4, service.ScheduleId);
        Assert.Equal(StandardSpeed, service.SpeedId);
        Assert.Equal(4037, service.JobTypeId);
    }

    [Fact]
    public async Task A_scheduled_standard_row_ignores_the_economy_client_flags()
    {
        await using var db = await SeededAsync(economyRuns: false);
        var (svc, _) = NewService(db, Row(jobTypeId: 4037, name: "Standard"));

        Assert.Single(await Get(svc));
    }

    [Fact]
    public async Task A_non_standard_speed_is_denied_even_when_the_client_runs_economy()
    {
        await using var db = await SeededAsync(economyRuns: false);
        var (svc, _) = NewService(db, Row(jobTypeId: 37, name: "Economy Run", speed: "ER"));

        Assert.Empty(await Get(svc));
    }

    [Fact]
    public async Task A_book_date_is_converted_from_tenant_local_to_utc()
    {
        await using var db = await SeededAsync();
        var (svc, _) = NewService(db,
            Row(jobTypeId: 4037, bookDate: new DateTime(2026, 9, 10, 14, 0, 0)));

        // 2pm NZST on 10 Sep is 02:00 UTC the same day
        Assert.Equal(
            new DateTime(2026, 9, 10, 2, 0, 0, DateTimeKind.Utc),
            Assert.Single(await Get(svc)).BookDateUtc);
    }

    [Fact]
    public async Task A_row_with_no_book_date_carries_none()
    {
        await using var db = await SeededAsync();
        var (svc, _) = NewService(db, Row(bookDate: null));

        Assert.Null(Assert.Single(await Get(svc)).BookDateUtc);
    }

    [Fact]
    public async Task A_row_with_no_job_type_id_is_ignored()
    {
        await using var db = await SeededAsync();
        var (svc, _) = NewService(db, Row(jobTypeId: null));

        Assert.Empty(await Get(svc));
    }

    [Fact]
    public async Task Nothing_is_offered_for_an_unknown_job_and_the_proc_is_not_called()
    {
        await using var db = await SeededAsync();
        var (svc, query) = NewService(db, Row());

        var services = await svc.GetAllowedServicesAsync(
            9999, Address(), NowUtc, TestContext.Current.CancellationToken);

        Assert.Empty(services);
        await query.DidNotReceiveWithAnyArgs()
            .ExecuteAsync(null!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task The_job_and_address_reach_the_proc_as_the_booking_app_sends_them()
    {
        await using var db = await SeededAsync();
        var (svc, query) = NewService(db, Row());

        await Get(svc, Address("Riccarton", "8041", latitude: -43.53m, longitude: 172.58m));

        await query.Received(1).ExecuteAsync(
            Arg.Is<AvailableServicesRequest>(r =>
                r.ClientId == ClientId
                && r.SizeName == "Car"
                && r.FromSuburb == "Airport"
                && r.FromPostCode == 2022
                && r.ToSuburb == "Riccarton"
                && r.ToPostCode == 8041
                && r.ToLatitude == -43.53m
                && r.ToLongitude == 172.58m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task The_proc_is_asked_in_tenant_local_time()
    {
        await using var db = await SeededAsync();
        var (svc, query) = NewService(db, Row());

        await Get(svc);

        await query.Received(1).ExecuteAsync(
            Arg.Is<AvailableServicesRequest>(r =>
                r.AsOfLocal == new DateTime(2026, 9, 10, 8, 0, 0)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_unparseable_postcode_is_sent_as_null_rather_than_zero()
    {
        await using var db = await SeededAsync();
        var (svc, query) = NewService(db, Row());

        await Get(svc, Address(postCode: "not-a-postcode"));

        await query.Received(1).ExecuteAsync(
            Arg.Is<AvailableServicesRequest>(r => r.ToPostCode == null),
            Arg.Any<CancellationToken>());
    }
}
