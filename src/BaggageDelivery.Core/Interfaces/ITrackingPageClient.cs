using BaggageDelivery.Core.Http.Models;

namespace BaggageDelivery.Core.Interfaces;

// Customer-facing tracking data lives in the trackingpage app (the backend
// for tracking.{domain}). It reads the Despatch DB directly, runs without
// auth (the encrypted-ID endpoint is the gate for customer use), and
// already handles live + archived + bulk job fall-through. BaggageDelivery
// calls it server-to-server.
public interface ITrackingPageClient
{
    // Returns the tracking snapshot for a job. Null when trackingpage
    // reports the job doesn't exist OR when the call fails transiently
    // (logged) — callers degrade gracefully either way.
    Task<TrackingDto?> GetJobAsync(int jobId, CancellationToken ct);
}
