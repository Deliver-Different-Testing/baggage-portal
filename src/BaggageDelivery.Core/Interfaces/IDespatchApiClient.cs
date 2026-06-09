using BaggageDelivery.Core.Http.Models;

namespace BaggageDelivery.Core.Interfaces;

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

    Task<TrackingDto?> GetJobTrackingAsync(int tenantId, string connection, string timeZone,
        int? clientId, int contactId, int jobId, CancellationToken ct);
}
