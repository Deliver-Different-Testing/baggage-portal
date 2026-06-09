using Microsoft.Extensions.Options;

namespace BaggageDelivery.Core.MagicLink;

public class MagicLinkOptions
{
    public const string SectionName = "MagicLink";

    public int ConfirmTtlDays { get; set; } = 7;
    public int TrackTtlDays { get; set; } = 7;
    public int TrackHardCapDays { get; set; } = 30;
    public string PublicBaseUrl { get; set; } = "";
    public string IssuerName { get; set; } = "BaggageDelivery";
}

public class MagicLinkOptionsValidator : IValidateOptions<MagicLinkOptions>
{
    public ValidateOptionsResult Validate(string? name, MagicLinkOptions options)
    {
        if (options.ConfirmTtlDays <= 0)
        {
            return ValidateOptionsResult.Fail("ConfirmTtlDays must be positive");
        }

        if (options.TrackHardCapDays < options.TrackTtlDays)
        {
            return ValidateOptionsResult.Fail("TrackHardCapDays must be >= TrackTtlDays");
        }

        if (string.IsNullOrWhiteSpace(options.PublicBaseUrl))
        {
            return ValidateOptionsResult.Fail("PublicBaseUrl is required");
        }

        return ValidateOptionsResult.Success;
    }
}
