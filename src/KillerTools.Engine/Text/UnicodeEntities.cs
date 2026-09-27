using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace KillerTools.Engine.Text;

public static partial class UnicodeEntities
{
    public static string Encode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var output = new StringBuilder(text.Length * 7);
        foreach (var value in text)
        {
            output.Append("&#");
            output.Append(((int)value).ToString(CultureInfo.InvariantCulture));
            output.Append(';');
        }

        return output.ToString();
    }

    public static string Decode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return DecimalEntity().Replace(text, static match =>
        {
            var value = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            return value <= char.MaxValue ? ((char)value).ToString() : match.Value;
        });
    }

    [GeneratedRegex("&#(\\d+);")]
    private static partial Regex DecimalEntity();
}

