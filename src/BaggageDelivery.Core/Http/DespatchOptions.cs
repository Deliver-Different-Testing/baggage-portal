namespace BaggageDelivery.Core.Http;

// Tenant identity for this deployment — one BaggageDelivery deployment per
// Despatch tenant (matches inboundagent + trackingpage). The values get
// baked into the SC-JWT claim minted in-flight by DespatchApiClient so the
// api repo can route to the right tenant DB.
public sealed class DespatchOptions
{
    public const string SectionName = "Despatch";

    public int TenantId { get; set; }
    public string Connection { get; set; } = "";
    public string TimeZone { get; set; } = "";
}
