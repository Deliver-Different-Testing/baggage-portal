using BaggageDelivery.Core.Enums;
using BaggageDelivery.Core.Interfaces;
using BaggageDelivery.Core.Models;
using BaggageDelivery.Core.MultiTenant;
using BaggageDelivery.Core.Services;
using BaggageDelivery.UnitTests.Helpers;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace BaggageDelivery.UnitTests.Services;

public class OrphanReconciliationServiceTests
{
    private static readonly DateTime Now = new(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime OutsideGrace = Now.AddHours(-2);

    [Fact]
    public async Task NotFound_marks_booking_orphaned_and_fails_pending_children()
    {
        var db = InMemoryDb.NewContext();
        var trackingPage = Substitute.For<ITrackingPageClient>();
        var tenants = Substitute.For<ITenantResolver>();
        var time = new FakeTimeProvider(Now);

        tenants.ResolveAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new TenantContext(1, "Despatch_1", "Pacific/Auckland"));
        trackingPage.CheckJobExistsAsync(0, CancellationToken.None)
            .ReturnsForAnyArgs(JobExistenceResult.NotFound);

        var bookingId = SeedBooking(db, tenantId: 1, jobId: 42, createdAtUtc: OutsideGrace);
        SeedPendingOutbox(db, bookingId, jobId: 42, tenantId: 1);
        SeedPendingNotification(db, bookingId);

        var svc = new OrphanReconciliationService(db, trackingPage, tenants, time);
        await svc.ReconcileOnceAsync(10, CancellationToken.None);

        var booking = db.BagDelBookings.AsNoTracking().Single(b => b.Id == bookingId);
        Assert.NotNull(booking.OrphanedAtUtc);
        Assert.Equal(Now, booking.OrphanedAtUtc);
        Assert.Contains("not found", booking.OrphanedReason!, StringComparison.OrdinalIgnoreCase);

        var outbox = db.BagDelConfirmationOutboxes.AsNoTracking().Single(o => o.BookingId == bookingId);
        Assert.Equal(OutboxStatus.Failed, outbox.Status);
        Assert.Contains("orphan", outbox.LastError!, StringComparison.OrdinalIgnoreCase);

        var notification = db.BagDelNotificationLogs.AsNoTracking().Single(n => n.BookingId == bookingId);
        Assert.Equal(NotificationStatus.Failed, notification.Status);
    }

    [Fact]
    public async Task Exists_leaves_booking_alone()
    {
        var db = InMemoryDb.NewContext();
        var trackingPage = Substitute.For<ITrackingPageClient>();
        var tenants = Substitute.For<ITenantResolver>();
        var time = new FakeTimeProvider(Now);

        tenants.ResolveAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new TenantContext(1, "Despatch_1", "Pacific/Auckland"));
        trackingPage.CheckJobExistsAsync(0, CancellationToken.None)
            .ReturnsForAnyArgs(JobExistenceResult.Exists);

        var bookingId = SeedBooking(db, tenantId: 1, jobId: 42, createdAtUtc: OutsideGrace);
        SeedPendingOutbox(db, bookingId, jobId: 42, tenantId: 1);

        var svc = new OrphanReconciliationService(db, trackingPage, tenants, time);
        await svc.ReconcileOnceAsync(10, CancellationToken.None);

        var booking = db.BagDelBookings.AsNoTracking().Single(b => b.Id == bookingId);
        Assert.Null(booking.OrphanedAtUtc);

        var outbox = db.BagDelConfirmationOutboxes.AsNoTracking().Single(o => o.BookingId == bookingId);
        Assert.Equal(OutboxStatus.Pending, outbox.Status);
    }

    [Fact]
    public async Task Unknown_leaves_booking_alone_for_next_cycle()
    {
        var db = InMemoryDb.NewContext();
        var trackingPage = Substitute.For<ITrackingPageClient>();
        var tenants = Substitute.For<ITenantResolver>();
        var time = new FakeTimeProvider(Now);

        tenants.ResolveAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new TenantContext(1, "Despatch_1", "Pacific/Auckland"));
        trackingPage.CheckJobExistsAsync(0, CancellationToken.None)
            .ReturnsForAnyArgs(JobExistenceResult.Unknown);

        var bookingId = SeedBooking(db, tenantId: 1, jobId: 42, createdAtUtc: OutsideGrace);

        var svc = new OrphanReconciliationService(db, trackingPage, tenants, time);
        await svc.ReconcileOnceAsync(10, CancellationToken.None);

        var booking = db.BagDelBookings.AsNoTracking().Single(b => b.Id == bookingId);
        Assert.Null(booking.OrphanedAtUtc);
    }

    [Fact]
    public async Task Booking_inside_grace_period_is_skipped()
    {
        var db = InMemoryDb.NewContext();
        var trackingPage = Substitute.For<ITrackingPageClient>();
        var tenants = Substitute.For<ITenantResolver>();
        var time = new FakeTimeProvider(Now);

        var bookingId = SeedBooking(db, tenantId: 1, jobId: 42, createdAtUtc: Now.AddMinutes(-5));

        var svc = new OrphanReconciliationService(db, trackingPage, tenants, time);
        await svc.ReconcileOnceAsync(10, CancellationToken.None);

        await trackingPage.DidNotReceiveWithAnyArgs().CheckJobExistsAsync(0, CancellationToken.None);
        var booking = db.BagDelBookings.AsNoTracking().Single(b => b.Id == bookingId);
        Assert.Null(booking.OrphanedAtUtc);
    }

    [Fact]
    public async Task Already_synced_booking_is_skipped()
    {
        var db = InMemoryDb.NewContext();
        var trackingPage = Substitute.For<ITrackingPageClient>();
        var tenants = Substitute.For<ITenantResolver>();
        var time = new FakeTimeProvider(Now);

        var bookingId = SeedBooking(
            db, tenantId: 1, jobId: 42, createdAtUtc: OutsideGrace, despatchSyncedAtUtc: OutsideGrace);

        var svc = new OrphanReconciliationService(db, trackingPage, tenants, time);
        await svc.ReconcileOnceAsync(10, CancellationToken.None);

        await trackingPage.DidNotReceiveWithAnyArgs().CheckJobExistsAsync(0, CancellationToken.None);
        var booking = db.BagDelBookings.AsNoTracking().Single(b => b.Id == bookingId);
        Assert.Null(booking.OrphanedAtUtc);
    }

    [Fact]
    public async Task Already_orphaned_booking_is_skipped()
    {
        var db = InMemoryDb.NewContext();
        var trackingPage = Substitute.For<ITrackingPageClient>();
        var tenants = Substitute.For<ITenantResolver>();
        var time = new FakeTimeProvider(Now);

        var bookingId = SeedBooking(
            db, tenantId: 1, jobId: 42, createdAtUtc: OutsideGrace, orphanedAtUtc: OutsideGrace);

        var svc = new OrphanReconciliationService(db, trackingPage, tenants, time);
        await svc.ReconcileOnceAsync(10, CancellationToken.None);

        await trackingPage.DidNotReceiveWithAnyArgs().CheckJobExistsAsync(0, CancellationToken.None);
        // Existing orphan timestamp is unchanged.
        var booking = db.BagDelBookings.AsNoTracking().Single(b => b.Id == bookingId);
        Assert.Equal(OutsideGrace, booking.OrphanedAtUtc);
    }

    [Fact]
    public async Task Unconfigured_tenant_skipped_without_probing()
    {
        var db = InMemoryDb.NewContext();
        var trackingPage = Substitute.For<ITrackingPageClient>();
        var tenants = Substitute.For<ITenantResolver>();
        var time = new FakeTimeProvider(Now);

        tenants.ResolveAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns<TenantContext>(_ => throw new InvalidOperationException("not configured"));

        SeedBooking(db, tenantId: 99, jobId: 42, createdAtUtc: OutsideGrace);

        var svc = new OrphanReconciliationService(db, trackingPage, tenants, time);
        await svc.ReconcileOnceAsync(10, CancellationToken.None);

        await trackingPage.DidNotReceiveWithAnyArgs().CheckJobExistsAsync(0, CancellationToken.None);
    }

    private static int SeedBooking(
        BaggageDeliveryContext db,
        int tenantId,
        int jobId,
        DateTime createdAtUtc,
        DateTime? despatchSyncedAtUtc = null,
        DateTime? orphanedAtUtc = null)
    {
        var booking = new BagDelBooking
        {
            JobId = jobId,
            TenantId = tenantId,
            CreatedAtUtc = createdAtUtc,
            DespatchSyncedAtUtc = despatchSyncedAtUtc,
            OrphanedAtUtc = orphanedAtUtc
        };
        db.BagDelBookings.Add(booking);
        db.SaveChanges();
        return booking.Id;
    }

    private static void SeedPendingOutbox(BaggageDeliveryContext db, int bookingId, int jobId, int tenantId)
    {
        db.BagDelConfirmationOutboxes.Add(new BagDelConfirmationOutbox
        {
            BookingId = bookingId,
            JobId = jobId,
            TenantId = tenantId,
            Status = OutboxStatus.Pending,
            NextAttemptUtc = Now,
            CreatedAtUtc = Now
        });
        db.SaveChanges();
    }

    private static void SeedPendingNotification(BaggageDeliveryContext db, int bookingId)
    {
        db.BagDelNotificationLogs.Add(new BagDelNotificationLog
        {
            BookingId = bookingId,
            Channel = NotificationChannel.Sms,
            Recipient = "+64211234567",
            Status = NotificationStatus.Pending,
            NextAttemptUtc = Now,
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now
        });
        db.SaveChanges();
    }
}
