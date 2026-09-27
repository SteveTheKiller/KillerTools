using System.Globalization;

namespace KillerTools.Engine.Security;

public readonly record struct PasswordStrengthResult(double Entropy, int CharsetLength, int PasswordLength, string CrackDurationFormatted, double SecondsToCrack, double Score);

public static class PasswordStrength
{
    private static readonly (string Unit, double Seconds, string Plural)[] TimeUnits =
    [
        ("millenium", 31_536_000_000, "millennia"), ("century", 3_153_600_000, "centuries"),
        ("decade", 315_360_000, "decades"), ("year", 31_536_000, "years"),
        ("month", 2_592_000, "months"), ("week", 604_800, "weeks"),
        ("day", 86_400, "days"), ("hour", 3_600, "hours"),
        ("minute", 60, "minutes"), ("second", 1, "seconds"),
    ];

    public static PasswordStrengthResult Analyze(string password, double guessesPerSecond = 1e9)
    {
        var charsetLength = GetCharsetLength(password);
        var entropy = password.Length == 0 ? 0 : System.Math.Log2(charsetLength) * password.Length;
        var secondsToCrack = System.Math.Pow(2, entropy) / guessesPerSecond;
        return new(entropy, charsetLength, password.Length, FormatDuration(secondsToCrack), secondsToCrack, System.Math.Min(entropy / 128, 1));
    }

    public static int GetCharsetLength(string password)
    {
        var length = 0;
        if (password.Any(character => character is >= 'a' and <= 'z')) length += 26;
        if (password.Any(character => character is >= 'A' and <= 'Z')) length += 26;
        if (password.Any(character => character is >= '0' and <= '9')) length += 10;
        if (password.Any(character => character is not (>= 'a' and <= 'z') and not (>= 'A' and <= 'Z') and not (>= '0' and <= '9'))) length += 32;
        return length;
    }

    private static string FormatDuration(double seconds)
    {
        if (seconds <= 0.001) return "Instantly";
        if (seconds <= 1) return "Less than a second";
        var parts = new List<string>(2);
        foreach (var (unit, secondsInUnit, plural) in TimeUnits)
        {
            var quantity = System.Math.Floor(seconds / secondsInUnit);
            seconds %= secondsInUnit;
            if (quantity <= 0) continue;
            var formatted = unit == "millenium" ? FormatMillennia(quantity) : quantity.ToString("0", CultureInfo.InvariantCulture);
            parts.Add($"{formatted} {(quantity > 1 ? plural : unit)}");
            if (parts.Count == 2) break;
        }
        return string.Join(", ", parts);
    }

    private static string FormatMillennia(double value)
    {
        var raw = value.ToString("G15", CultureInfo.InvariantCulture).Replace("E", "e", StringComparison.Ordinal);
        if (raw.Contains('e')) return raw;
        return value % 1 == 0 ? value.ToString("N0", CultureInfo.GetCultureInfo("en-US")) : value.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
