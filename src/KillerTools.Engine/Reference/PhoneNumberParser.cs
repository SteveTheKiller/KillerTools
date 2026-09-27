using System.Globalization;
using System.Text.Json.Serialization;
using PhoneNumbers;

namespace KillerTools.Engine.Reference;

public sealed record PhoneNumberDetails(
    [property: JsonPropertyName("countryCode")] string? CountryCode,
    [property: JsonPropertyName("country")] string? Country,
    [property: JsonPropertyName("callingCode")] string CallingCode,
    [property: JsonPropertyName("isValid")] bool IsValid,
    [property: JsonPropertyName("isPossible")] bool IsPossible,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("international")] string International,
    [property: JsonPropertyName("national")] string National,
    [property: JsonPropertyName("e164")] string E164,
    [property: JsonPropertyName("rfc3966")] string Rfc3966);

public static class PhoneNumberParser
{
    private static readonly PhoneNumberUtil PhoneNumbers = PhoneNumberUtil.GetInstance();

    public static IReadOnlySet<string> SupportedCountries { get; } = PhoneNumbers.GetSupportedRegions();

    public static PhoneNumberDetails Parse(string phone, string? defaultCountry = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phone);
        if (defaultCountry is not null && !SupportedCountries.Contains(defaultCountry))
        {
            throw new ArgumentException("Unknown default country code", nameof(defaultCountry));
        }

        var parsed = PhoneNumbers.Parse(phone, defaultCountry);
        var region = PhoneNumbers.GetRegionCodeForNumber(parsed);
        var e164 = PhoneNumbers.Format(parsed, PhoneNumberFormat.E164);
        return new PhoneNumberDetails(
            region,
            CountryName(region),
            $"+{parsed.CountryCode}",
            PhoneNumbers.IsValidNumber(parsed),
            PhoneNumbers.IsPossibleNumber(parsed),
            NumberType(PhoneNumbers.GetNumberType(parsed)),
            PhoneNumbers.Format(parsed, PhoneNumberFormat.INTERNATIONAL).Replace('-', ' '),
            PhoneNumbers.Format(parsed, PhoneNumberFormat.NATIONAL),
            e164,
            $"tel:{e164}");
    }

    private static string? CountryName(string? region)
    {
        if (string.IsNullOrEmpty(region) || region == "001") return null;
        try { return new RegionInfo(region).EnglishName; }
        catch (ArgumentException) { return null; }
    }

    private static string NumberType(PhoneNumberType type) => type switch
    {
        PhoneNumberType.UNKNOWN => "Unknown",
        _ => type.ToString(),
    };
}
