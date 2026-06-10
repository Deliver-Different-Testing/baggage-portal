using BaggageDelivery.Core.Http.Models;

namespace BaggageDelivery.Core.Interfaces;

// Despatch WebAPICore client — write operations against tucJob (release /
// cancel / send-on-hold / update delivery). Tenant identity comes from
// DespatchOptions (one deployment per tenant); the client mints an SC-JWT
// in-flight using those values. Read operations for customer tracking go
// to trackingpage via ITrackingPageClient.
public interface IDespatchApiClient
{
    Task<bool> ReleaseBaggageJobAsync(BookingReleaseRequest request, CancellationToken ct);

    Task<bool> CancelBaggageJobAsync(BookingCancelRequest request, CancellationToken ct);

    Task<bool> SendOnHoldAsync(SendOnHoldBookingRequest request, CancellationToken ct);

    Task<bool> UpdateJobDeliveryAsync(int jobId, DeliveryUpdateRequest request, CancellationToken ct);

    // PATCH api/Jobs/{jobId}/status — sets tucJob.UcjbStatus and writes a
    // JobDeliveryJourney audit row atomically on the api side.
    Task<bool> UpdateJobStatusAsync(int jobId, JobStatusUpdateRequest request, CancellationToken ct);
}
