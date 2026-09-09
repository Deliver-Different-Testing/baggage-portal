using BaggageDelivery.Core.Interfaces;
using BaggageDelivery.Core.Models;

namespace BaggageDelivery.Core.Services;

internal sealed class AvailableServicesQuery(BaggageDeliveryContext db) : IAvailableServicesQuery
{
    // Baggage is always a single item and the proc only uses weight and item count for rate
    // maths. @Kms stays null to match what the api repo sends for a non-regional, non-CLICO
    // client — it gates the nationwide block and Afternoon Home, so it is not incidental.
    private const string BaggageWeight = "20";
    private const int BaggageItems = 1;
    private static readonly decimal? NoKms = null;

    public async Task<IReadOnlyList<BagDel_stpAvailableServicesResult>> ExecuteAsync(
        AvailableServicesRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await db.Procedures.BagDel_stpAvailableServicesAsync(
            clientID: request.ClientId,
            size: request.SizeName,
            fromSuburb: request.FromSuburb,
            toSuburb: request.ToSuburb,
            weight: BaggageWeight,
            items: BaggageItems,
            dateTime: request.AsOfLocal,
            kms: NoKms,
            fromPostCode: request.FromPostCode,
            toPostCode: request.ToPostCode,
            pickup: null,
            dropoff: null,
            overSizeItems: null,
            overWeightItems: null,
            privateRes: null,
            dangerousGoods: null,
            truckStartTime: null,
            truckHours: null,
            ourRef: null,
            clientRefA: null,
            clientRefB: null,
            cubicList: null,
            weightList: null,
            stockSizeId: null,
            fromLatitude: null,
            fromLongitude: null,
            toLatitude: request.ToLatitude,
            toLongitude: request.ToLongitude,
            cancellationToken: ct);
    }
}
