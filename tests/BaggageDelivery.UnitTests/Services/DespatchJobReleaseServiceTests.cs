using BaggageDelivery.Core.Http;
using BaggageDelivery.Core.Http.Models;
using BaggageDelivery.Core.Models.Entities;
using BaggageDelivery.Core.MultiTenant;
using BaggageDelivery.Core.Services;
using BaggageDelivery.UnitTests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace BaggageDelivery.UnitTests.Services;

public class DespatchJobReleaseServiceTests
{
    [Fact]
    public async Task Drain_calls_UpdateDelivery_then_Release_and_marks_Done()
    {
        var db = InMemoryDb.NewContext();
        var despatch = Substitute.For<IDespatchApiClient>();
        var tenants = Substitute.For<ITenantResolver>();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc));

        tenants.ResolveAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new TenantContext(1, "Despatch_1", "Pacific/Auckland"));

        despatch.UpdateJobDeliveryAsync(0, "", "", 0, 0, 0, null!, CancellationToken.None)
            .ReturnsForAnyArgs(Task.FromResult(true));
        despatch.ReleaseBaggageJobAsync(0, "", "", 0, 0, null!, CancellationToken.None)
            .ReturnsForAnyArgs(Task.FromResult(true));

        SeedPendingOutbox(db, time);

        var svc = new DespatchJobReleaseService(db, despatch, tenants, time,
            NullLogger<DespatchJobReleaseService>.Instance);

        await svc.DrainOnceAsync(10, CancellationToken.None);

        var row = db.ConfirmationOutbox.First();
        Assert.Equal(OutboxStatus.Done, row.Status);
        Received.InOrder(() =>
        {
            despatch.UpdateJobDeliveryAsync(1, "Despatch_1", "Pacific/Auckland",
                null, 0, 42, Arg.Any<DeliveryUpdateRequest>(), Arg.Any<CancellationToken>());
            despatch.ReleaseBaggageJobAsync(1, "Despatch_1", "Pacific/Auckland",
                null, 0, Arg.Any<BookingReleaseRequest>(), Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Drain_failure_backs_off_with_attempt_increment()
    {
        var db = InMemoryDb.NewContext();
        var despatch = Substitute.For<IDespatchApiClient>();
        var tenants = Substitute.For<ITenantResolver>();
        var time = new FakeTimeProvider(new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc));

        tenants.ResolveAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new TenantContext(1, "Despatch_1", "Pacific/Auckland"));
        despatch.UpdateJobDeliveryAsync(0, "", "", 0, 0, 0, null!, CancellationToken.None)
            .ReturnsForAnyArgs(Task.FromResult(false));

        SeedPendingOutbox(db, time);

        var svc = new DespatchJobReleaseService(db, despatch, tenants, time,
            NullLogger<DespatchJobReleaseService>.Instance);

        await svc.DrainOnceAsync(10, CancellationToken.None);

        var row = db.ConfirmationOutbox.First();
        Assert.Equal(OutboxStatus.Pending, row.Status);
        Assert.Equal(1, row.AttemptCount);
        Assert.True(row.NextAttemptUtc > time.GetUtcNow().UtcDateTime);
        Assert.NotNull(row.LastError);
    }

    private static void SeedPendingOutbox(Core.Models.BaggageDeliveryContext db, FakeTimeProvider time)
    {
        var booking = new BagDelBooking
        {
            JobId = 42,
            TenantId = 1,
            CreatedAtUtc = time.GetUtcNow().UtcDateTime,
            ConfirmedAtUtc = time.GetUtcNow().UtcDateTime,
            AddressLine1 = "1 Queen St",
            City = "Auckland",
            Country = "NZ",
            TimeSlotStartUtc = time.GetUtcNow().UtcDateTime.AddHours(2),
            TimeSlotEndUtc = time.GetUtcNow().UtcDateTime.AddHours(5),
            AtlOption = AtlOption.FrontDoor
        };
        db.Bookings.Add(booking);
        db.SaveChanges();

        db.ConfirmationOutbox.Add(new BagDelConfirmationOutbox
        {
            BookingId = booking.Id,
            JobId = 42,
            TenantId = 1,
            Status = OutboxStatus.Pending,
            NextAttemptUtc = time.GetUtcNow().UtcDateTime,
            CreatedAtUtc = time.GetUtcNow().UtcDateTime
        });
        db.SaveChanges();
    }
}
