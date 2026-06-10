namespace BaggageDelivery.Core.Http;

public sealed class TrackingPageUrlsOptions
{
    public const string SectionName = "TrackingPageUrls";

    public Uri? BaseUrl { get; set; }
}
