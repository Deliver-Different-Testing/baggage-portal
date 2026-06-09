namespace BaggageDelivery.Core.Http;

public sealed class DespatchUrlsOptions
{
    public const string SectionName = "DespatchUrls";

    public Uri? ApiBaseUrl { get; set; }
}
