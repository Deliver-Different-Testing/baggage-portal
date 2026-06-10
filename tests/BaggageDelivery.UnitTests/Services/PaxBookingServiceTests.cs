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

public class PaxBookingServiceTests
{
    // GetSummary's tucJob read isn't unit-testable here — InMemoryDb
    // deliberately Ignores TucJob to keep the test schema lean. The
    // not-found and happy paths are covered at the integration layer.

    private static IOptions<DespatchOptions> DespatchOpts() =>
        Options.Create(new DespatchOptions { TenantId = 1, Connection = "TEST", TimeZone = "Pacific/Auckland" });

    [Fact]
    public async Task Confirm_calls_api_update_then_release()
    {
        await using var db = InMemoryDb.NewContext();
        var despatch = Substitute.For<IDespatchApiClient>();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc));

        despatch.UpdateJobDeliveryAsync(0, null!, CancellationToken.None).ReturnsForAnyArgs(true);
        despatch.ReleaseBaggageJobAsync(null!, CancellationToken.None).ReturnsForAnyArgs(true);

        var svc = new PaxBookingService(db, despatch, DespatchOpts(), time);

        await svc.ConfirmAsync(new ConfirmBookingInput(
            JobId: 42,
            Address: new AddressUpdateDto { Line1 = "1 Queen St", City = "Auckland", Country = "NZ" },
            TimeSlotStartUtc: time.GetUtcNow().UtcDateTime.AddHours(2),
            TimeSlotEndUtc: time.GetUtcNow().UtcDateTime.AddHours(5),
            AtlOption: AtlOption.FrontDoor,
            AccessNotes: null,
            PassengerName: "Jane Pax",
            PassengerPhone: "+64211234567",
            PassengerEmail: "jane@example.com"), CancellationToken.None);

        Received.InOrder(() =>
        {
            despatch.UpdateJobDeliveryAsync(42, Arg.Any<DeliveryUpdateRequest>(), Arg.Any<CancellationToken>());
            despatch.ReleaseBaggageJobAsync(Arg.Any<BookingReleaseRequest>(), Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Confirm_forwards_passenger_fields_to_delivery_update()
    {
        await using var db = InMemoryDb.NewContext();
        var despatch = Substitute.For<IDespatchApiClient>();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc));

        despatch.UpdateJobDeliveryAsync(0, null!, CancellationToken.None).ReturnsForAnyArgs(true);
        despatch.ReleaseBaggageJobAsync(null!, CancellationToken.None).ReturnsForAnyArgs(true);

        var svc = new PaxBookingService(db, despatch, DespatchOpts(), time);

        await svc.ConfirmAsync(new ConfirmBookingInput(
            JobId: 42,
            Address: new AddressUpdateDto { Line1 = "1 Queen St", City = "Auckland", Country = "NZ" },
            TimeSlotStartUtc: time.GetUtcNow().UtcDateTime.AddHours(2),
            TimeSlotEndUtc: time.GetUtcNow().UtcDateTime.AddHours(5),
            AtlOption: AtlOption.FrontDoor,
            AccessNotes: "Buzzer 4B",
            PassengerName: "Jane Pax",
            PassengerPhone: "+64211234567",
            PassengerEmail: "jane@example.com"), CancellationToken.None);

        await despatch.Received(1).UpdateJobDeliveryAsync(
            42,
            Arg.Is<DeliveryUpdateRequest>(r =>
                r.PassengerName == "Jane Pax"
                && r.PassengerPhone == "+64211234567"
                && r.PassengerEmail == "jane@example.com"
                && r.AccessNotes == "Buzzer 4B"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Confirm_throws_when_update_returns_false()
    {
        await using var db = InMemoryDb.NewContext();
        var despatch = Substitute.For<IDespatchApiClient>();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc));

        despatch.UpdateJobDeliveryAsync(0, null!, CancellationToken.None).ReturnsForAnyArgs(false);

        var svc = new PaxBookingService(db, despatch, DespatchOpts(), time);

        var input = new ConfirmBookingInput(
            JobId: 42,
            new AddressUpdateDto { Line1 = "x", City = "y", Country = "NZ" },
            time.GetUtcNow().UtcDateTime, time.GetUtcNow().UtcDateTime.AddHours(1),
            AtlOption.None, null,
            PassengerName: "Jane",
            PassengerPhone: null,
            PassengerEmail: null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ConfirmAsync(input, CancellationToken.None));

        await despatch.DidNotReceiveWithAnyArgs().ReleaseBaggageJobAsync(null!, CancellationToken.None);
    }

    [Fact]
    public async Task GetTimeslots_builds_consecutive_run_pairs_from_first_eco_setting()
    {
        await using var db = InMemoryDb.NewContext();
        var despatch = Substitute.For<IDespatchApiClient>();
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

        var svc = new PaxBookingService(db, despatch, DespatchOpts(), time);

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
        var despatch = Substitute.For<IDespatchApiClient>();
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

        var svc = new PaxBookingService(db, despatch, DespatchOpts(), time);

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
        var despatch = Substitute.For<IDespatchApiClient>();
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

        var svc = new PaxBookingService(db, despatch, DespatchOpts(), time);

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
        var despatch = Substitute.For<IDespatchApiClient>();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc));

        var svc = new PaxBookingService(db, despatch, DespatchOpts(), time);

        var slots = await svc.GetTimeslotsAsync(
            jobId: 42,
            localDate: new DateTime(2026, 6, 10),
            CancellationToken.None);

        Assert.Empty(slots);
    }

    [Fact]
    public async Task GetTimeslots_marks_first_slot_whose_end_is_future_as_first_available()
    {
        await using var db = InMemoryDb.NewContext();
        var despatch = Substitute.For<IDespatchApiClient>();
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

        var svc = new PaxBookingService(db, despatch, DespatchOpts(), time);

        var slots = await svc.GetTimeslotsAsync(
            jobId: 42,
            localDate: new DateTime(2026, 6, 10),
            CancellationToken.None);

        Assert.False(slots[0].FirstAvailable);
        Assert.False(slots[1].FirstAvailable);
        Assert.True(slots[2].FirstAvailable);
    }

    [Fact]
    public async Task Confirm_throws_when_release_returns_false()
    {
        await using var db = InMemoryDb.NewContext();
        var despatch = Substitute.For<IDespatchApiClient>();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc));

        despatch.UpdateJobDeliveryAsync(0, null!, CancellationToken.None).ReturnsForAnyArgs(true);
        despatch.ReleaseBaggageJobAsync(null!, CancellationToken.None).ReturnsForAnyArgs(false);

        var svc = new PaxBookingService(db, despatch, DespatchOpts(), time);

        var input = new ConfirmBookingInput(
            JobId: 42,
            new AddressUpdateDto { Line1 = "x", City = "y", Country = "NZ" },
            time.GetUtcNow().UtcDateTime, time.GetUtcNow().UtcDateTime.AddHours(1),
            AtlOption.None, null,
            PassengerName: "Jane",
            PassengerPhone: null,
            PassengerEmail: null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ConfirmAsync(input, CancellationToken.None));
    }
}
