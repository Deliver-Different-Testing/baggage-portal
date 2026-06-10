using BaggageDelivery.Core.Http.Models;
using BaggageDelivery.Core.Interfaces;
using BaggageDelivery.Core.Models;
using BaggageDelivery.Core.Services;
using BaggageDelivery.UnitTests.Helpers;
using NSubstitute;
using Xunit;

namespace BaggageDelivery.UnitTests.Services;

public class PaxBookingServiceTests
{
    [Fact]
    public async Task GetSummary_returns_null_when_trackingpage_has_no_data()
    {
        var trackingPage = Substitute.For<ITrackingPageClient>();
        var despatch = Substitute.For<IDespatchApiClient>();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc));

        trackingPage.GetJobAsync(0, CancellationToken.None).ReturnsForAnyArgs((TrackingDto?)null);

        var svc = new PaxBookingService(trackingPage, despatch, time);
        var result = await svc.GetSummaryAsync(42, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetSummary_maps_tracking_to_booking_summary()
    {
        var trackingPage = Substitute.For<ITrackingPageClient>();
        var despatch = Substitute.For<IDespatchApiClient>();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc));

        trackingPage.GetJobAsync(0, CancellationToken.None).ReturnsForAnyArgs(new TrackingDto
        {
            JobId = 42,
            CurrentStatus = "1",
            Events = [],
            EtaWindowStartUtc = new DateTime(2026, 6, 10, 14, 0, 0, DateTimeKind.Utc),
            EtaWindowEndUtc = new DateTime(2026, 6, 10, 17, 0, 0, DateTimeKind.Utc),
            CourierFirstName = "Air NZ"
        });

        var svc = new PaxBookingService(trackingPage, despatch, time);
        var result = await svc.GetSummaryAsync(42, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(42, result.JobId);
        Assert.Equal("Air NZ", result.AirlineLabel);
        Assert.Equal(new DateTime(2026, 6, 10, 14, 0, 0, DateTimeKind.Utc), result.EarliestSlotUtc);
        Assert.Equal(new DateTime(2026, 6, 10, 17, 0, 0, DateTimeKind.Utc), result.LatestSlotUtc);
    }

    [Fact]
    public async Task Confirm_calls_api_update_then_release()
    {
        var trackingPage = Substitute.For<ITrackingPageClient>();
        var despatch = Substitute.For<IDespatchApiClient>();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc));

        despatch.UpdateJobDeliveryAsync(0, null!, CancellationToken.None).ReturnsForAnyArgs(true);
        despatch.ReleaseBaggageJobAsync(null!, CancellationToken.None).ReturnsForAnyArgs(true);

        var svc = new PaxBookingService(trackingPage, despatch, time);

        await svc.ConfirmAsync(new ConfirmBookingInput(
            JobId: 42,
            Address: new AddressUpdateDto { Line1 = "1 Queen St", City = "Auckland", Country = "NZ" },
            TimeSlotStartUtc: time.GetUtcNow().UtcDateTime.AddHours(2),
            TimeSlotEndUtc: time.GetUtcNow().UtcDateTime.AddHours(5),
            AtlOption: AtlOption.FrontDoor,
            AccessNotes: null, PhoneOverride: null), CancellationToken.None);

        Received.InOrder(() =>
        {
            despatch.UpdateJobDeliveryAsync(42, Arg.Any<DeliveryUpdateRequest>(), Arg.Any<CancellationToken>());
            despatch.ReleaseBaggageJobAsync(Arg.Any<BookingReleaseRequest>(), Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Confirm_throws_when_update_returns_false()
    {
        var trackingPage = Substitute.For<ITrackingPageClient>();
        var despatch = Substitute.For<IDespatchApiClient>();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc));

        despatch.UpdateJobDeliveryAsync(0, null!, CancellationToken.None).ReturnsForAnyArgs(false);

        var svc = new PaxBookingService(trackingPage, despatch, time);

        var input = new ConfirmBookingInput(
            JobId: 42,
            new AddressUpdateDto { Line1 = "x", City = "y", Country = "NZ" },
            time.GetUtcNow().UtcDateTime, time.GetUtcNow().UtcDateTime.AddHours(1),
            AtlOption.None, null, null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ConfirmAsync(input, CancellationToken.None));

        await despatch.DidNotReceiveWithAnyArgs().ReleaseBaggageJobAsync(null!, CancellationToken.None);
    }

    [Fact]
    public async Task Confirm_throws_when_release_returns_false()
    {
        var trackingPage = Substitute.For<ITrackingPageClient>();
        var despatch = Substitute.For<IDespatchApiClient>();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc));

        despatch.UpdateJobDeliveryAsync(0, null!, CancellationToken.None).ReturnsForAnyArgs(true);
        despatch.ReleaseBaggageJobAsync(null!, CancellationToken.None).ReturnsForAnyArgs(false);

        var svc = new PaxBookingService(trackingPage, despatch, time);

        var input = new ConfirmBookingInput(
            JobId: 42,
            new AddressUpdateDto { Line1 = "x", City = "y", Country = "NZ" },
            time.GetUtcNow().UtcDateTime, time.GetUtcNow().UtcDateTime.AddHours(1),
            AtlOption.None, null, null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ConfirmAsync(input, CancellationToken.None));
    }
}
