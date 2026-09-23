namespace BaggageDelivery.Core.Security;

public sealed class BookingLinkOptions
{
    public const string SectionName = "BookingLinks";

    public string PublicBaseUrl { get; set; } = string.Empty;
}
