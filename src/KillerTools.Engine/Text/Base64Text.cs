using System.Text;
using System.Text.RegularExpressions;

namespace KillerTools.Engine.Text;

public static partial class Base64Text
{
    public static string Encode(string text, bool urlSafe = false)
    {
        ArgumentNullException.ThrowIfNull(text);
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
        return urlSafe ? encoded.TrimEnd('=').Replace('+', '-').Replace('/', '_') : encoded;
    }

    public static string Decode(string encoded, bool urlSafe = false)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        var clean = DataPrefix().Replace(encoded, string.Empty);
        if (urlSafe)
        {
            clean = NonUrlSafeBase64().Replace(clean.Replace('-', '+').Replace('_', '/'), string.Empty);
            clean = clean.PadRight((clean.Length + 3) / 4 * 4, '=');
        }
        else
        {
            clean = Whitespace().Replace(clean, string.Empty);
        }

        return Encoding.UTF8.GetString(Convert.FromBase64String(clean));
    }

    [GeneratedRegex("^data:.*?;base64,")]
    private static partial Regex DataPrefix();

    [GeneratedRegex("[^A-Z0-9+/]", RegexOptions.IgnoreCase)]
    private static partial Regex NonUrlSafeBase64();

    [GeneratedRegex("\\s")]
    private static partial Regex Whitespace();
}
