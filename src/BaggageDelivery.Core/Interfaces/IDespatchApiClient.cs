using BaggageDelivery.Core.Http.Models;

namespace BaggageDelivery.Core.Interfaces;

// Despatch WebAPICore client — used only for write operations that mutate
// tucJob state (release / cancel / send-on-hold / update delivery). All
// calls carry an SC-JWT minted in-flight. READ operations for customer
// tracking go to trackingpage via ITrackingPageClient.
public interface IDespatchApiClient
{
    Task<bool> ReleaseBaggageJobAsync(int tenantId, string connection, string timeZone,
        int? clientId, int contactId, BookingReleaseRequest request, CancellationToken ct);

    Task<bool> CancelBaggageJobAsync(int tenantId, string connection, string timeZone,
        int? clientId, int contactId, BookingCancelRequest request, CancellationToken ct);

    Task<bool> SendOnHoldAsync(int tenantId, string connection, string timeZone,
        int? clientId, int contactId, SendOnHoldBookingRequest request, CancellationToken ct);

    Task<bool> UpdateJobDeliveryAsync(int tenantId, string connection, string timeZone,
        int? clientId, int contactId, int jobId, DeliveryUpdateRequest request, CancellationToken ct);
}
