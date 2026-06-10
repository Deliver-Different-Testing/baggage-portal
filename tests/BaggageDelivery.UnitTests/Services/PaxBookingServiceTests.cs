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
    public async Task Confirm_updates_booking_row_in_place_and_queues_outbox()
    {
        var db = InMemoryDb.NewContext();
        var trackingPage = Substitute.For<ITrackingPageClient>();
        var tenants = Substitute.For<ITenantResolver>();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc));

        var booking = new BagDelBooking
        {
            JobId = 42,
            TenantId = 1,
            CreatedAtUtc = time.GetUtcNow().UtcDateTime
        };
        db.BagDelBookings.Add(booking);
        await db.SaveChangesAsync(CancellationToken.None);

        var svc = new PaxBookingService(db, trackingPage, tenants, time);

        await svc.ConfirmAsync(new ConfirmBookingInput(
            TenantId: booking.TenantId,
            JobId: booking.JobId,
            Address: new AddressUpdateDto { Line1 = "1 Queen St", City = "Auckland", Country = "NZ" },
            TimeSlotStartUtc: time.GetUtcNow().UtcDateTime.AddHours(2),
            TimeSlotEndUtc: time.GetUtcNow().UtcDateTime.AddHours(5),
            AtlOption: AtlOption.FrontDoor,
            AccessNotes: null, PhoneOverride: null), CancellationToken.None);

        var updated = db.BagDelBookings.AsQueryable().First(b => b.Id == booking.Id);
        Assert.NotNull(updated.ConfirmedAtUtc);
        Assert.Equal("1 Queen St", updated.AddressLine1);
        Assert.Equal(AtlOption.FrontDoor, updated.AtlOption);
        Assert.Single(db.BagDelConfirmationOutboxes);
        Assert.Equal(OutboxStatus.Pending, db.BagDelConfirmationOutboxes.First().Status);
        Assert.Equal(booking.Id, db.BagDelConfirmationOutboxes.First().BookingId);
    }

    [Fact]
    public async Task Confirming_same_booking_twice_throws()
    {
        var db = InMemoryDb.NewContext();
        var trackingPage = Substitute.For<ITrackingPageClient>();
        var tenants = Substitute.For<ITenantResolver>();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc));

        var booking = new BagDelBooking
        {
            JobId = 1,
            TenantId = 1,
            CreatedAtUtc = time.GetUtcNow().UtcDateTime
        };
        db.BagDelBookings.Add(booking);
        await db.SaveChangesAsync(CancellationToken.None);

        var svc = new PaxBookingService(db, trackingPage, tenants, time);

        var input = new ConfirmBookingInput(
            TenantId: booking.TenantId,
            JobId: booking.JobId,
            new AddressUpdateDto { Line1 = "x", City = "y", Country = "NZ" },
            time.GetUtcNow().UtcDateTime, time.GetUtcNow().UtcDateTime.AddHours(1),
            AtlOption.None, null, null);

        await svc.ConfirmAsync(input, CancellationToken.None);

        await Assert.ThrowsAsync<ConfirmationAlreadyExistsException>(
            () => svc.ConfirmAsync(input, CancellationToken.None));
    }

    [Fact]
    public async Task Confirm_creates_booking_lazily_when_missing()
    {
        var db = InMemoryDb.NewContext();
        var trackingPage = Substitute.For<ITrackingPageClient>();
        var tenants = Substitute.For<ITenantResolver>();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc));

        var svc = new PaxBookingService(db, trackingPage, tenants, time);

        var input = new ConfirmBookingInput(
            TenantId: 1,
            JobId: 9999,
            new AddressUpdateDto { Line1 = "x", City = "y", Country = "NZ" },
            time.GetUtcNow().UtcDateTime, time.GetUtcNow().UtcDateTime.AddHours(1),
            AtlOption.None, null, null);

        await svc.ConfirmAsync(input, CancellationToken.None);

        var created = db.BagDelBookings.AsQueryable().Single();
        Assert.Equal(1, created.TenantId);
        Assert.Equal(9999, created.JobId);
        Assert.NotNull(created.ConfirmedAtUtc);
    }
}
