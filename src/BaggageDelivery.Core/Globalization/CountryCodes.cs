using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace BaggageDelivery.Core.Globalization;

public static class CountryCodes
{
    private static readonly FrozenDictionary<string, string> Iso2ToIso3;

    private static readonly FrozenDictionary<string, string> Aliases;

    private static readonly (string Alias, string Iso2)[] CuratedAliases =
    [
        ("UK", "GB"),
        ("GREATBRITAIN", "GB"),
        ("BRITAIN", "GB"),
        ("ENGLAND", "GB"),
        ("UNITEDSTATESOFAMERICA", "US"),
        ("AMERICA", "US"),
        ("HOLLAND", "NL"),
        ("AOTEAROA", "NZ")
    ];

    static CountryCodes()
    {
        var iso2ToIso3 = new Dictionary<string, string>(StringComparer.Ordinal);
        var aliases = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
        {
            RegionInfo region;
            try
            {
                region = new RegionInfo(culture.Name);
            }
            catch (ArgumentException)
            {
                continue;
            }

            var iso2 = region.TwoLetterISORegionName.ToUpperInvariant();
            if (iso2.Length != 2)
            {
                continue;
            }

            var iso3 = region.ThreeLetterISORegionName.ToUpperInvariant();

            iso2ToIso3[iso2] = iso3;

            aliases[iso2] = iso2;
            aliases[Normalise(iso3)] = iso2;
            aliases[Normalise(region.EnglishName)] = iso2;
        }

        foreach (var (alias, iso2) in CuratedAliases)
        {
            aliases[alias] = iso2;
        }

        Iso2ToIso3 = iso2ToIso3.ToFrozenDictionary(StringComparer.Ordinal);
        Aliases = aliases.ToFrozenDictionary(StringComparer.Ordinal);
    }

    public static int KnownCountryCount => Iso2ToIso3.Count;

    public static bool TryToIso2(string? value, [NotNullWhen(true)] out string? iso2)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return Aliases.TryGetValue(Normalise(value), out iso2);
        }

        iso2 = null;
        return false;
    }

    public static string? ToIso3(string? value) =>
        TryToIso2(value, out var iso2) ? Iso2ToIso3[iso2] : null;

    public static bool IsUnitedStates(string? value) =>
        TryToIso2(value, out var iso2) && iso2 == "US";

    private static string Normalise(string value)
    {
        var buffer = new char[value.Length];
        var length = 0;

        foreach (var ch in value.Where(char.IsLetter))
        {
            buffer[length++] = char.ToUpperInvariant(ch);
        }

        return new string(buffer, 0, length);
    }
}
