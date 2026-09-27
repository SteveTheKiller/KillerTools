using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace KillerTools.Engine.Text;

public static partial class AsciiBinary
{
    public static string Encode(string text, string separator = " ")
    {
        ArgumentNullException.ThrowIfNull(text);
        return string.Join(separator, text.Select(value => Convert.ToString(value, 2).PadLeft(8, '0')));
    }

    public static string Decode(string binary)
    {
        ArgumentNullException.ThrowIfNull(binary);
        var clean = NonBinary().Replace(binary, string.Empty);
        if (clean.Length % 8 != 0)
        {
            throw new FormatException("Invalid binary string");
        }

        var output = new StringBuilder(clean.Length / 8);
        for (var index = 0; index < clean.Length; index += 8)
        {
            output.Append((char)int.Parse(clean.AsSpan(index, 8), NumberStyles.BinaryNumber, CultureInfo.InvariantCulture));
        }
        return output.ToString();
    }

    [GeneratedRegex("[^01]")]
    private static partial Regex NonBinary();
}
