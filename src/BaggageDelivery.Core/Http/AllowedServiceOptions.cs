using Microsoft.Extensions.Options;

namespace BaggageDelivery.Core.Http;

public sealed class AllowedServiceOptions
{
    public const string SectionName = "Despatch:AllowedServices";

    public bool Enabled { get; set; }

    public int[] DeniedJobTypeIds { get; set; } = [10];

    public string[] DeniedSystemNames { get; set; } = ["UT", "MR"];

    public bool AllowScheduledServices { get; set; } = true;

    public string UnserviceableAddressNotifyEmail { get; set; } = string.Empty;
}

internal sealed class AllowedServiceOptionsValidator : IValidateOptions<AllowedServiceOptions>
{
    public ValidateOptionsResult Validate(string? name, AllowedServiceOptions options)
    {
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        if (string.IsNullOrWhiteSpace(options.UnserviceableAddressNotifyEmail))
        {
            return ValidateOptionsResult.Fail(
                "UnserviceableAddressNotifyEmail must be set when AddressGuardRailsEnabled is true "
                + "— without it a passenger whose address cannot be serviced has no way to reach "
                + "the airline.");
        }

        return ValidateOptionsResult.Success;
    }
}
