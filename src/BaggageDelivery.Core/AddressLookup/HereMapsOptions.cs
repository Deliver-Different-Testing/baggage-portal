namespace BaggageDelivery.Core.AddressLookup;

public sealed class HereMapsOptions
{
    public const string SectionName = "HereMaps";

    public string ApiKey { get; init; } = string.Empty;
}
