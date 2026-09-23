using BaggageDelivery.Core.Http.Models;

namespace BaggageDelivery.Core.Services;

internal static class DespatchAddressComposer
{
    private const int MaxLength = 150;

    public static string Compose(AddressUpdateDto address, string country)
    {
        ArgumentNullException.ThrowIfNull(address);

        return Compose(
            address.Line1, address.Line2, address.Line3, address.Line4,
            address.Line5, address.Line6, address.Line7, country);
    }

    public static string Compose(params string?[] parts)
    {
        ArgumentNullException.ThrowIfNull(parts);

        var joined = string.Join(", ", parts
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!.Trim()));

        return joined.Length <= MaxLength ? joined : joined[..MaxLength].TrimEnd(' ', ',');
    }
}
