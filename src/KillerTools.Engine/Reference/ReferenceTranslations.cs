namespace KillerTools.Engine.Reference;

public static class ReferenceTranslations
{
    private static readonly string[] Locales = ["bn", "cs", "de", "es", "fr", "hu", "it", "ja", "kk", "no", "pl", "pt", "ru", "tr", "uk", "vi", "zh", "zh-TW"];
    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>>> Catalogs = new(Load);
    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>> GroupPolicyCatalogs = new(() => Locales.ToDictionary(
        locale => locale,
        locale => (IReadOnlyDictionary<string, string>)EmbeddedCatalog.DeserializeBrotliBase64<Dictionary<string, string>>($"KillerTools.Engine.Data.group-policy-{locale}.br64"),
        StringComparer.OrdinalIgnoreCase));

    public static IReadOnlyList<string> SupportedLocales => Locales;

    public static string Translate(string tool, string? locale, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tool);
        ArgumentNullException.ThrowIfNull(value);
        if (string.IsNullOrWhiteSpace(locale) || locale.Equals("en", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        var normalized = Locales.FirstOrDefault(item => item.Equals(locale, StringComparison.OrdinalIgnoreCase));
        if (normalized is null)
        {
            return value;
        }

        var translations = tool.Equals("group-policy-reference", StringComparison.Ordinal)
            ? GroupPolicyCatalogs.Value[normalized]
            : Catalogs.Value[normalized].GetValueOrDefault(tool);
        return translations is not null
            && translations.TryGetValue(value, out var translated)
            ? translated
            : value;
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>> Load()
    {
        return Locales.ToDictionary(
            locale => locale,
            locale => (IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>)EmbeddedCatalog.DeserializeBrotliBase64<Dictionary<string, IReadOnlyDictionary<string, string>>>($"KillerTools.Engine.Data.reference-{locale}.br64"),
            StringComparer.OrdinalIgnoreCase);
    }
}
