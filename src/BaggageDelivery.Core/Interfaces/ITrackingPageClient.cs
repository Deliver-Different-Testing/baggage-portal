using BaggageDelivery.Core.Enums;
using BaggageDelivery.Core.Http.Models;

namespace BaggageDelivery.Core.Interfaces;

// Customer-facing tracking data lives in the trackingpage app (the backend
// for tracking.{domain}). It reads the Despatch DB directly, runs without
// auth (the encrypted-ID endpoint is the gate for customer use), and
// already handles live + archived + bulk job fall-through. BaggageDelivery
// calls it server-to-server for both pax summary rendering and
// orphan-reconciliation probes.
public interface ITrackingPageClient
{
    // Returns the tracking snapshot for a job. Null when trackingpage
    // definitively reports the job doesn't exist; null also on transient
    // failures (logged) so callers degrade to row-only data.
    Task<TrackingDto?> GetJobAsync(int jobId, CancellationToken ct);

    // Tri-state probe used by OrphanReconciliationService. Unknown is
    // distinct from NotFound so transient 5xx / circuit-breaker trips never
    // flip a real booking to orphaned.
    Task<JobExistenceResult> CheckJobExistsAsync(int jobId, CancellationToken ct);
}
