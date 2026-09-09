using BaggageDelivery.Core.Http.Models;
using BaggageDelivery.Core.Services;

namespace BaggageDelivery.Core.Interfaces;

public interface IServiceAvailabilityService
{
    Task<IReadOnlyList<CandidateService>> GetAllowedServicesAsync(
        int jobId, AddressUpdateDto address, DateTime asOfUtc, CancellationToken ct);
}
