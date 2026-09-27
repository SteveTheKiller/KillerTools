using System.Globalization;

namespace KillerTools.Engine.Math;

public static class Percentages
{
    public static string Of(double percent, double total) =>
        IsFinite(percent, total) ? Format(percent / 100 * total) : string.Empty;

    public static string Ratio(double part, double total) =>
        IsFinite(part, total) && total != 0 ? $"{Format(100 * part / total)}%" : string.Empty;

    public static string Change(double from, double to)
    {
        if (!IsFinite(from, to) || from == 0)
        {
            return string.Empty;
        }

        var value = (to - from) / from * 100;
        return $"{(value >= 0 ? "+" : string.Empty)}{Format(value)}%";
    }

    private static bool IsFinite(double first, double second) =>
        double.IsFinite(first) && double.IsFinite(second);

    private static string Format(double value) =>
        System.Math.Round(value, 8, MidpointRounding.AwayFromZero)
            .ToString("0.########", CultureInfo.InvariantCulture);
}

