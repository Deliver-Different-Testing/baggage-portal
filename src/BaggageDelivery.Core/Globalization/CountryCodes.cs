using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace BaggageDelivery.Core.Globalization;

// Resolves the free-text country values held in legacy Despatch address columns
// (tucJob.DeliveryAddressLine8 is nvarchar(255) and contains "New Zealand",
// "USA", "NZL", "NZ", ...) to a canonical ISO-3166-1 alpha-2 code.
//
// The bulk of the table is derived from RegionInfo, which supplies alpha-2,
// alpha-3 and the English name for ~250 regions. A curated alias table is
// overlaid on top for the colloquial spellings RegionInfo has no concept of
// ("UK", "Great Britain", "Holland").
public static class CountryCodes
{
    private static readonly FrozenDictionary<string, string> Iso2ToIso3;
    private static readonly FrozenDictionary<string, string> Iso2ToName;

    // Every recognised spelling (normalised) -> ISO-2.
    private static readonly FrozenDictionary<string, string> Aliases;

    // Colloquial and historic spellings RegionInfo.EnglishName never produces.
    // Keys are pre-normalised (letters only, upper-cased).
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
        var iso2ToName = new Dictionary<string, string>(StringComparer.Ordinal);
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
                // Not every specific culture has a region (e.g. neutral-ish or
                // custom cultures) — RegionInfo throws rather than returning null.
                continue;
            }

            var iso2 = region.TwoLetterISORegionName.ToUpperInvariant();
            if (iso2.Length != 2)
            {
                continue;
            }

            var iso3 = region.ThreeLetterISORegionName.ToUpperInvariant();

            iso2ToIso3[iso2] = iso3;
            iso2ToName[iso2] = region.EnglishName;

            aliases[iso2] = iso2;
            aliases[Normalise(iso3)] = iso2;
            aliases[Normalise(region.EnglishName)] = iso2;
        }

        // Curated entries win over anything RegionInfo produced.
        foreach (var (alias, iso2) in CuratedAliases)
        {
            aliases[alias] = iso2;
        }

        Iso2ToIso3 = iso2ToIso3.ToFrozenDictionary(StringComparer.Ordinal);
        Iso2ToName = iso2ToName.ToFrozenDictionary(StringComparer.Ordinal);
        Aliases = aliases.ToFrozenDictionary(StringComparer.Ordinal);
    }

    // Number of regions resolved from RegionInfo. Guards against the table
    // silently emptying under InvariantGlobalization or an ICU-less base image.
    public static int KnownCountryCount => Iso2ToIso3.Count;

    // Resolves an ISO-2, ISO-3, English name or curated alias to ISO-2.
    // Deliberately has no fallback parameter: an unrecognised value is a
    // decision for the caller, not something to paper over with a default.
    public static bool TryToIso2(string? value, [NotNullWhen(true)] out string? iso2)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            iso2 = null;
            return false;
        }

        // An unassigned two-letter code such as "XX" must not pass through, so
        // membership in the table is the test — not the string's length.
        return Aliases.TryGetValue(Normalise(value), out iso2);
    }

    // ISO-3 for the HereMaps `in=countryCode:` filter. Null when unresolvable,
    // so callers drop the entry rather than sending HereMaps a malformed filter.
    public static string? ToIso3(string? value) =>
        TryToIso2(value, out var iso2) ? Iso2ToIso3[iso2] : null;

    public static bool IsUnitedStates(string? value) =>
        TryToIso2(value, out var iso2) && iso2 == "US";

    // Canonical English name — the escape hatch if a downstream Despatch consumer
    // turns out to render DeliveryAddressLine8 as a display value.
    public static string? ToDisplayName(string? value) =>
        TryToIso2(value, out var iso2) ? Iso2ToName[iso2] : null;

    // Letters only, upper-cased: collapses "  new zealand  ", "New Zealand" and
    // "N.Z."-style punctuation into a single comparable key.
    private static string Normalise(string value)
    {
        var buffer = new char[value.Length];
        var length = 0;

        foreach (var ch in value)
        {
            if (char.IsLetter(ch))
            {
                buffer[length++] = char.ToUpperInvariant(ch);
            }
        }

        return new string(buffer, 0, length);
    }
}
