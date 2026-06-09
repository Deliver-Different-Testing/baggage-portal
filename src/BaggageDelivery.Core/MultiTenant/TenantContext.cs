namespace BaggageDelivery.Core.MultiTenant;

public sealed record TenantContext(int TenantId, string Connection, string TimeZone);
