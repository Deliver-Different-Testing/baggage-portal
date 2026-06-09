using Microsoft.Extensions.Configuration;

namespace BaggageDelivery.Core.MultiTenant;

// v1 implementation: tenant directory lives in configuration under
// "Tenants:{id}:{Connection|TimeZone}". A future revision should hit the
// shared Hub partner-directory service the way IntegrationManager does.
internal sealed class ConfigTenantResolver(IConfiguration configuration) : ITenantResolver
{
    public Task<TenantContext> ResolveAsync(int tenantId, CancellationToken ct)
    {
        var section = configuration.GetSection($"Tenants:{tenantId}");
        var connection = section["Connection"]
            ?? throw new InvalidOperationException($"Tenants:{tenantId}:Connection is not configured");
        var timeZone = section["TimeZone"]
            ?? throw new InvalidOperationException($"Tenants:{tenantId}:TimeZone is not configured");
        return Task.FromResult(new TenantContext(tenantId, connection, timeZone));
    }
}
