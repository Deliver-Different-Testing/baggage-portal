using BaggageDelivery.Core.Interfaces;
using BaggageDelivery.Core.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace BaggageDelivery.Core.Services;

internal sealed class SuburbResolver(BaggageDeliveryContext db) : ISuburbResolver
{
    private const int FallbackUnknownSuburbId = 152;

    public async Task<int?> ResolveAsync(string? suburbName, string? postCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(suburbName))
        {
            return null;
        }

        var parsedPostCode = int.TryParse(postCode, out var zip) && zip > 0 ? zip : (int?)null;

        try
        {
            var unknownId = await UnknownSuburbIdAsync(ct) ?? FallbackUnknownSuburbId;
            var resolved = await LookupAsync(suburbName, parsedPostCode, ct);

            if (!IsUnresolved(resolved, unknownId))
            {
                return IsUnresolved(resolved, unknownId) ? null : resolved;
            }

            var abbreviated = Abbreviate(suburbName);
            if (!string.Equals(abbreviated, suburbName, StringComparison.Ordinal))
            {
                resolved = await LookupAsync(abbreviated, parsedPostCode, ct);
            }

            return IsUnresolved(resolved, unknownId) ? null : resolved;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Warning(ex, "Could not resolve suburb {Suburb} {PostCode}", suburbName, postCode);
            return null;
        }
    }

    private static bool IsUnresolved(int? suburbId, int unknownId) =>
        suburbId is null or 0 || suburbId == unknownId;

    private Task<int?> LookupAsync(string suburbName, int? postCode, CancellationToken ct) =>
        db.TucJobs
            .Select(_ => BaggageDeliveryContext.UTL_fncSuburb_FromNameWithPostCode(
                suburbName, null, postCode))
            .FirstOrDefaultAsync(ct);

    private Task<int?> UnknownSuburbIdAsync(CancellationToken ct) =>
        db.TucSuburbs
            .Where(s => s.UcsuName == "Unknown")
            .Select(s => (int?)s.UcsuId)
            .FirstOrDefaultAsync(ct);

    private static string Abbreviate(string suburbName) => suburbName
        .Replace("Saint", "St", StringComparison.OrdinalIgnoreCase)
        .Replace("Mount", "Mt", StringComparison.OrdinalIgnoreCase)
        .Replace("Point", "Pt", StringComparison.OrdinalIgnoreCase);
}
