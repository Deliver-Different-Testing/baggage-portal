using BaggageDelivery.Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace BaggageDelivery.Core.MultiTenant;

// Decorator over an ITenantResolver that memoises tenant lookups.
// Today the underlying ConfigTenantResolver is cheap (in-memory IConfiguration),
// but when this swaps to a Hub partner-directory client (per ConfigTenantResolver
// notes) every outbox drain iteration would otherwise become an extra HTTP call.
// The decorator stays correct either way — registered now so the cache layer
// is in place before the slow underlying implementation lands.
internal sealed class CachingTenantResolver(ITenantResolver inner, IMemoryCache cache) : ITenantResolver
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);

    public async Task<TenantContext> ResolveAsync(int tenantId, CancellationToken ct)
    {
        var key = $"tenant:{tenantId}";
        if (cache.TryGetValue<TenantContext>(key, out var hit) && hit is not null)
        {
            return hit;
        }

        var resolved = await inner.ResolveAsync(tenantId, ct);
        cache.Set(key, resolved, CacheTtl);
        return resolved;
    }
}
