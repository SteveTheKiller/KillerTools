using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KillerTools.Engine.Security;

public sealed record JwtDecodeResult(JsonElement Header, JsonElement Payload, IReadOnlyList<IReadOnlyDictionary<string, object?>> HeaderClaims, IReadOnlyList<IReadOnlyDictionary<string, object?>> PayloadClaims, bool SignatureVerified);

public static partial class JwtParser
{
    private static readonly JsonSerializerOptions PrettyJson = new() { WriteIndented = true, IndentSize = 3 };
    private static readonly IReadOnlyDictionary<string, string> AlgorithmDescriptions = new Dictionary<string, string>
    {
        ["HS256"] = "HMAC using SHA-256", ["HS384"] = "HMAC using SHA-384", ["HS512"] = "HMAC using SHA-512",
        ["RS256"] = "RSASSA-PKCS1-v1_5 using SHA-256", ["RS384"] = "RSASSA-PKCS1-v1_5 using SHA-384", ["RS512"] = "RSASSA-PKCS1-v1_5 using SHA-512",
        ["ES256"] = "ECDSA using P-256 and SHA-256", ["ES384"] = "ECDSA using P-384 and SHA-384", ["ES512"] = "ECDSA using P-521 and SHA-512",
        ["PS256"] = "RSASSA-PSS using SHA-256 and MGF1 with SHA-256", ["PS384"] = "RSASSA-PSS using SHA-384 and MGF1 with SHA-384", ["PS512"] = "RSASSA-PSS using SHA-512 and MGF1 with SHA-512",
        ["none"] = "No digital signature or MAC performed",
    };
    private static readonly IReadOnlyDictionary<string, string> ClaimDescriptions = new Dictionary<string, string>
    {
        ["typ"] = "Type", ["alg"] = "Algorithm", ["iss"] = "Issuer", ["sub"] = "Subject", ["aud"] = "Audience", ["exp"] = "Expiration Time", ["nbf"] = "Not Before", ["iat"] = "Issued At", ["jti"] = "JWT ID",
        ["name"] = "Full name", ["given_name"] = "Given name(s) or first name(s)", ["family_name"] = "Surname(s) or last name(s)", ["middle_name"] = "Middle name(s)", ["nickname"] = "Casual name", ["preferred_username"] = "Shorthand name by which the End-User wishes to be referred to",
        ["profile"] = "Profile page URL", ["picture"] = "Profile picture URL", ["website"] = "Web page or blog URL", ["email"] = "Preferred e-mail address", ["email_verified"] = "True if the e-mail address has been verified; otherwise false", ["gender"] = "Gender", ["birthdate"] = "Birthday", ["zoneinfo"] = "Time zone", ["locale"] = "Locale", ["phone_number"] = "Preferred telephone number", ["phone_number_verified"] = "True if the phone number has been verified; otherwise false", ["address"] = "Preferred postal address", ["updated_at"] = "Time the information was last updated",
        ["azp"] = "Authorized party - the party to which the ID Token was issued", ["nonce"] = "Value used to associate a Client session with an ID Token", ["auth_time"] = "Time when the authentication occurred", ["at_hash"] = "Access Token hash value", ["c_hash"] = "Code hash value", ["acr"] = "Authentication Context Class Reference", ["amr"] = "Authentication Methods References", ["sub_jwk"] = "Public key used to check the signature of an ID Token", ["cnf"] = "Confirmation",
        ["sip_from_tag"] = "SIP From tag header field parameter value", ["sip_date"] = "SIP Date header field value", ["sip_callid"] = "SIP Call-Id header field value", ["sip_cseq_num"] = "SIP CSeq numeric header field parameter value", ["sip_via_branch"] = "SIP Via branch header field parameter value", ["orig"] = "Originating Identity String", ["dest"] = "Destination Identity String", ["mky"] = "Media Key Fingerprint String", ["events"] = "Security Events", ["toe"] = "Time of Event", ["txn"] = "Transaction Identifier", ["rph"] = "Resource Priority Header Authorization", ["sid"] = "Session ID", ["vot"] = "Vector of Trust value", ["vtm"] = "Vector of Trust trustmark URL", ["attest"] = "Attestation level as defined in SHAKEN framework", ["origid"] = "Originating Identifier as defined in SHAKEN framework", ["act"] = "Actor", ["scope"] = "Scope Values", ["client_id"] = "Client Identifier", ["may_act"] = "Authorized Actor - the party that is authorized to become the actor", ["jcard"] = "jCard data", ["at_use_nbr"] = "Number of API requests for which the access token can be used", ["div"] = "Diverted Target of a Call", ["opt"] = "Original PASSporT (in Full Form)", ["vc"] = "Verifiable Credential as specified in the W3C Recommendation", ["vp"] = "Verifiable Presentation as specified in the W3C Recommendation", ["sph"] = "SIP Priority header field", ["ace_profile"] = "ACE profile a token is supposed to be used with.", ["cnonce"] = "Client nonce", ["exi"] = "Expires in", ["roles"] = "Roles", ["groups"] = "Groups", ["entitlements"] = "Entitlements", ["token_introspection"] = "Token introspection response",
    };

    [GeneratedRegex("^[\\w-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex JwtPartPattern();

    public static JwtDecodeResult Decode(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            throw new FormatException("Invalid JWT structure");
        }
        var header = DecodePart(parts[0]);
        var payload = DecodePart(parts[1]);
        if (header.ValueKind != JsonValueKind.Object || payload.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException("Invalid JWT");
        }
        return new(header, payload, DescribeClaims(header), DescribeClaims(payload), false);
    }

    private static JsonElement DecodePart(string part)
    {
        if (part.Length > 8192 || !JwtPartPattern().IsMatch(part))
        {
            throw new FormatException("Invalid JWT part");
        }
        var padded = part.Replace('-', '+').Replace('_', '/').PadRight((part.Length + 3) / 4 * 4, '=');
        using var document = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(padded)));
        return document.RootElement.Clone();
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, object?>> DescribeClaims(JsonElement values) =>
        values.EnumerateObject().Select(property =>
        {
            var claim = new Dictionary<string, object?>
            {
                ["claim"] = property.Name,
                ["value"] = FormatValue(property.Value),
            };
            if (property.Name is "exp" or "nbf" or "iat" && property.Value.TryGetInt64(out var seconds))
            {
                var date = DateTimeOffset.FromUnixTimeSeconds(seconds).LocalDateTime;
                claim["friendlyValue"] = $"{date.ToString("d", CultureInfo.CurrentCulture)} {date.ToString("T", CultureInfo.CurrentCulture)}";
            }
            else if (property.Name == "alg" && property.Value.ValueKind == JsonValueKind.String && AlgorithmDescriptions.TryGetValue(property.Value.GetString()!, out var algorithm))
            {
                claim["friendlyValue"] = algorithm;
            }
            if (ClaimDescriptions.TryGetValue(property.Name, out var description))
            {
                claim["claimDescription"] = description;
            }
            return (IReadOnlyDictionary<string, object?>)claim;
        }).ToArray();

    private static string FormatValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Object or JsonValueKind.Array => JsonSerializer.Serialize(value, PrettyJson).Replace("\r\n", "\n", StringComparison.Ordinal),
        JsonValueKind.String => value.GetString()!,
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null => "null",
        _ => value.GetRawText(),
    };
}
