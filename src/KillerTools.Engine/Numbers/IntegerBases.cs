using System.Numerics;
using System.Text;

namespace KillerTools.Engine.Numbers;

public static class IntegerBases
{
    private const string Digits = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ+/";

    public static string Convert(string value, int fromBase, int toBase)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (fromBase is < 2 or > 64 || toBase is < 2 or > 64) throw new ArgumentOutOfRangeException(nameof(fromBase));

        var number = BigInteger.Zero;
        foreach (var digit in value)
        {
            var index = Digits.IndexOf(digit);
            if (index < 0 || index >= fromBase) throw new FormatException($"Invalid digit for base {fromBase}");
            number = number * fromBase + index;
        }

        if (number.IsZero) return "0";
        var output = new StringBuilder();
        while (number > 0)
        {
            number = BigInteger.DivRem(number, toBase, out var remainder);
            output.Insert(0, Digits[(int)remainder]);
        }
        return output.ToString();
    }
}
