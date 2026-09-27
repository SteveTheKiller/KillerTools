using KillerTools.Engine.Reference;

namespace KillerTools.Engine.Network;

public sealed record MacVendorResult(string Prefix, string? Details);

public static class MacVendorLookup
{
    private static readonly Lazy<IReadOnlyDictionary<string, string>> Catalog = new(() =>
        EmbeddedCatalog.DeserializeBrotliBase64<Dictionary<string, string>>(
            "KillerTools.Engine.Data.oui-data.br64"));

    public static MacVendorResult Lookup(string macAddress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(macAddress);
        if (macAddress.Length is < 6 or > 24
            || macAddress.Any(character => !Uri.IsHexDigit(character) && character is not '.' and not ':' and not '-'))
        {
            throw new FormatException("Invalid MAC address");
        }

        string cleaned = new(macAddress
            .Where(character => character is not '.' and not ':' and not '-')
            .Select(char.ToUpperInvariant)
            .ToArray());
        if (cleaned.Length is < 6 or > 12 || cleaned.Length % 2 != 0)
        {
            throw new FormatException("Invalid MAC address");
        }

        string prefix = cleaned[..6];
        return new MacVendorResult(prefix, Catalog.Value.GetValueOrDefault(prefix));
    }
}
