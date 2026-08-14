namespace BaggageDelivery.Core.Http;

// Tenant identity for this deployment — one BaggageDelivery deployment
// per Despatch tenant (matches inboundagent + trackingpage). TimeZone is
// the tenant's IANA zone (e.g. "Pacific/Auckland"), used for UTC↔local
// conversions on inbound pax submissions and timeslot generation.
// Countries seeds HereMaps address autocomplete and supplies the default
// country on the pax delivery address (first entry wins).
public sealed class DespatchOptions
{
    public const string SectionName = "Despatch";

    public string TimeZone { get; set; } = string.Empty;

    public string[] Countries { get; set; } = ["NZ"];

    // Shown to the passenger as "Need help? Call …" when the job's client has no
    // phone of its own on tucClient. Optional — the portal hides the line rather
    // than printing a dead "Need help?".
    public string SupportPhone { get; set; } = string.Empty;
}
