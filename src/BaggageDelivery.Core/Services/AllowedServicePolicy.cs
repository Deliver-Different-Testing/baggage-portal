using BaggageDelivery.Core.Http;

namespace BaggageDelivery.Core.Services;

public static class AllowedServicePolicy
{
    private const string DirectName = "DIRECT";
    private const string GreenColour = "#00FF00";
    private const string Economy = "EC";
    private const string EconomyRun = "ER";
    public const string StandardName = "Standard";

    public static IReadOnlyList<CandidateService> Filter(
        IEnumerable<CandidateService> candidates,
        AllowedServiceOptions options,
        ClientServiceFlags flags) =>
        [.. candidates.Where(c => IsAllowed(c, options, flags))];

    public static bool IsAllowed(
        CandidateService service, AllowedServiceOptions options, ClientServiceFlags flags)
    {
        if (!IsGreenAvailable(service))
        {
            return false;
        }

        if (string.Equals(service.Name, DirectName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (options.DeniedJobTypeIds.Contains(service.SpeedId))
        {
            return false;
        }

        if (Matches(options.DeniedSystemNames, service.SystemName))
        {
            return false;
        }

        if (!string.Equals(service.Name, StandardName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (service.IsScheduled)
        {
            return options.AllowScheduledServices;
        }

        return service.SystemName switch
        {
            Economy => flags.EconomyActive && !flags.EconomyRuns,
            EconomyRun => flags.EconomyActive && flags.EconomyRuns,
            _ => true
        };
    }

    // WS_stpJobType_Rates (reached via BagDel_stpAvailableServices) rewrites Possible/Unlikely
    // to 'Available' in its standard block but keeps the amber colour, so the verdict alone is
    // not enough. Nationwide rows are the other way round: 'Available' with no colour at all.
    private static bool IsGreenAvailable(CandidateService service)
    {
        if (!string.Equals(service.Availability, AvailabilityVerdict.Available,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var colour = service.AvailabilityColour?.Trim();
        return string.IsNullOrEmpty(colour)
               || string.Equals(colour, GreenColour, StringComparison.OrdinalIgnoreCase);
    }

    private static bool Matches(string[] names, string? systemName) =>
        systemName is not null && names.Contains(systemName, StringComparer.OrdinalIgnoreCase);
}
