using BaggageDelivery.Core.MultiTenant;

namespace BaggageDelivery.Core.Interfaces;

public interface ITenantResolver
{
    // Given a TenantId, return the (Connection, TimeZone) tuple needed to mint an SC-JWT
    // for Despatch WebAPICore. In v1 this reads from configuration; in production this
    // should reach into the tenant directory the way IntegrationManager does.
    Task<TenantContext> ResolveAsync(int tenantId, CancellationToken ct);
}
