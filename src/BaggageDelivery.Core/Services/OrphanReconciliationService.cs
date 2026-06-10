using BaggageDelivery.Core.Enums;
using BaggageDelivery.Core.Interfaces;
using BaggageDelivery.Core.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace BaggageDelivery.Core.Services;

// Hygiene job: a BagDelBooking.JobId is a soft reference to tucJob.ucjbID
// (CLAUDE.md — FK was dropped to keep BaggageDelivery and Despatch in their
// own bounded contexts). When a courier job is deleted/archived in Despatch
// we never hear about it, so this service periodically asks Despatch
// whether each un-synced booking's JobId still exists and marks the
// stragglers as orphaned. Marking also fails any pending child outbox rows
// so we don't keep retrying delivery updates against a vanished job.
//
// Definitive 404 ⇒ orphan. Any other failure (network, 5xx, circuit-breaker
// open) ⇒ Unknown ⇒ leave alone and try again next cycle. A booking is
// never flipped to orphaned based on a transient signal.
internal sealed class OrphanReconciliationService(
    BaggageDeliveryContext db,
    IDespatchApiClient despatch,
    ITenantResolver tenants,
    TimeProvider time) : IOrphanReconciliationService
{
    // Don't probe bookings created in the last GracePeriod — a passenger
    // could be mid-flow and we don't want to race the IM mint or pre-empt
    // a confirmation. 30 minutes is comfortably longer than any single-
    // session pax flow and well under any plausible "job archived" cadence.
    private static readonly TimeSpan GracePeriod = TimeSpan.FromMinutes(30);

    public async Task ReconcileOnceAsync(int batchSize, CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var cutoff = now - GracePeriod;

        var candidates = await db.BagDelBookings
            .AsNoTracking()
            .Where(b => b.OrphanedAtUtc == null
                        && b.DespatchSyncedAtUtc == null
                        && b.CreatedAtUtc < cutoff)
            .OrderBy(b => b.CreatedAtUtc)
            .Take(batchSize)
            .Select(b => new ReconcileCandidate(b.Id, b.TenantId, b.JobId))
            .ToListAsync(ct);

        if (candidates.Count == 0)
        {
            return;
        }

        // Group by tenant so we resolve the tenant context once per tenant,
        // not once per booking. CachingTenantResolver already memoises but
        // grouping also makes the per-tenant log line cleaner.
        foreach (var perTenant in candidates.GroupBy(c => c.TenantId))
        {
            TenantContextOrSkip resolved;
            try
            {
                var tenant = await tenants.ResolveAsync(perTenant.Key, ct);
                resolved = new TenantContextOrSkip(tenant.Connection, tenant.TimeZone);
            }
            catch (InvalidOperationException ex)
            {
                Log.Warning(ex,
                    "Reconcile: tenant {TenantId} is not configured — skipping {Count} bookings",
                    perTenant.Key, perTenant.Count());
                continue;
            }

            foreach (var candidate in perTenant)
            {
                await ProcessOneAsync(candidate, resolved, ct);
            }
        }
    }

    private async Task ProcessOneAsync(
        ReconcileCandidate candidate, TenantContextOrSkip tenant, CancellationToken ct)
    {
        JobExistenceResult result;
        try
        {
            result = await despatch.CheckJobExistsAsync(
                candidate.TenantId, tenant.Connection, tenant.TimeZone,
                clientId: null, contactId: 0, candidate.JobId, ct);
        }
        catch (Exception ex)
        {
            Log.Warning(ex,
                "Reconcile: CheckJobExists threw for BookingId={BookingId} JobId={JobId} — treating as Unknown",
                candidate.BookingId, candidate.JobId);
            return;
        }

        if (result != JobExistenceResult.NotFound)
        {
            return;
        }

        var now = time.GetUtcNow().UtcDateTime;
        const string reason = "Despatch job not found (404)";

        // Re-check OrphanedAtUtc IS NULL in the UPDATE WHERE clause so two
        // concurrent reconcilers can't double-mark; the second one no-ops.
        var marked = await db.BagDelBookings
            .Where(b => b.Id == candidate.BookingId && b.OrphanedAtUtc == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(b => b.OrphanedAtUtc, now)
                .SetProperty(b => b.OrphanedReason, reason), ct);

        if (marked == 0)
        {
            return;
        }

        // Stop the dispatch outbox from retrying against a vanished job.
        // Notification logs follow the same logic — there's nothing left to
        // confirm or track for a booking whose underlying courier job is gone.
        var outboxFailed = await db.BagDelConfirmationOutboxes
            .Where(o => o.BookingId == candidate.BookingId && o.Status == OutboxStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.Status, OutboxStatus.Failed)
                .SetProperty(o => o.LastError, $"Booking orphaned: {reason}"), ct);

        var notificationsFailed = await db.BagDelNotificationLogs
            .Where(n => n.BookingId == candidate.BookingId && n.Status == NotificationStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.Status, NotificationStatus.Failed)
                .SetProperty(n => n.LastError, $"Booking orphaned: {reason}")
                .SetProperty(n => n.UpdatedAtUtc, now), ct);

        Log.Warning(
            "Reconcile: BookingId={BookingId} JobId={JobId} TenantId={TenantId} marked orphaned. " +
            "Cancelled OutboxRows={OutboxFailed} NotificationRows={NotificationsFailed}",
            candidate.BookingId, candidate.JobId, candidate.TenantId, outboxFailed, notificationsFailed);
    }

    private sealed record ReconcileCandidate(int BookingId, int TenantId, int JobId);

    private sealed record TenantContextOrSkip(string Connection, string TimeZone);
}
