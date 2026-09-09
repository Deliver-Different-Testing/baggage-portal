using BaggageDelivery.Core.Models;
using BaggageDelivery.Core.Services;

namespace BaggageDelivery.Core.Interfaces;

public interface IAvailableServicesQuery
{
    Task<IReadOnlyList<BagDel_stpAvailableServicesResult>> ExecuteAsync(
        AvailableServicesRequest request, CancellationToken ct);
}
