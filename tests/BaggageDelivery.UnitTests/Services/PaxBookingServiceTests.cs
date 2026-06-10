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
