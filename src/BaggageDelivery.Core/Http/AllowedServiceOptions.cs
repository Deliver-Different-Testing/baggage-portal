using Microsoft.Extensions.Options;

namespace BaggageDelivery.Core.Http;

public sealed class AllowedServiceOptions
{
    public const string SectionName = "Despatch:AllowedServices";

    public bool Enabled { get; set; }

    public string[] AllowedSystemNames { get; set; } = ["EC", "ER"];

    public int[] AllowedJobTypeIds { get; set; } = [56, 94, 95, 96, 110, 120, 121];

    public string[] AllowedNames { get; set; } = ["Standard"];

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

        if (options.AllowedSystemNames.Length == 0
            && options.AllowedJobTypeIds.Length == 0
            && options.AllowedNames.Length == 0
            && !options.AllowScheduledServices)
        {
            return ValidateOptionsResult.Fail(
                "Despatch:AllowedServices allows nothing — every passenger who changes their "
                + "address would be told to contact the airline.");
        }

        return ValidateOptionsResult.Success;
    }
}
