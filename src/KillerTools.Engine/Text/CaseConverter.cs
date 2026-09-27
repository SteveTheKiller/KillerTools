using System.Globalization;
using System.Text.RegularExpressions;

namespace KillerTools.Engine.Text;

public sealed record CaseConversion(string Label, string Value);

public static partial class CaseConverter
{
    public static IReadOnlyList<CaseConversion> Convert(string input)
    {
        var words = Words(input);
        var lower = words.Select(word => word.ToLowerInvariant()).ToArray();
        string Capital(string word) => word.Length == 0 ? word : string.Concat(word[..1].ToUpperInvariant(), word[1..].ToLowerInvariant());
        return
        [
            new("Lowercase", input.ToLower(CultureInfo.CurrentCulture)),
            new("Uppercase", input.ToUpper(CultureInfo.CurrentCulture)),
            new("Camelcase", lower.Length == 0 ? "" : lower[0] + string.Concat(lower.Skip(1).Select(Capital))),
            new("Capitalcase", string.Join(" ", lower.Select(Capital))),
            new("Constantcase", string.Join("_", lower).ToUpperInvariant()),
            new("Dotcase", string.Join(".", lower)),
            new("Headercase", string.Join("-", lower.Select(Capital))),
            new("Nocase", string.Join(" ", lower)),
            new("Paramcase", string.Join("-", lower)),
            new("Pascalcase", string.Concat(lower.Select(Capital))),
            new("Pathcase", string.Join("/", lower)),
            new("Sentencecase", lower.Length == 0 ? "" : Capital(lower[0]) + (lower.Length > 1 ? " " + string.Join(" ", lower.Skip(1)) : "")),
            new("Snakecase", string.Join("_", lower)),
            new("Mockingcase", string.Concat(input.Select((character, index) => index % 2 == 0 ? char.ToUpper(character, CultureInfo.CurrentCulture) : char.ToLower(character, CultureInfo.CurrentCulture)))),
        ];
    }

    private static string[] Words(string input)
    {
        var separated = LowerUpperBoundary().Replace(AcronymBoundary().Replace(input, "$1 $2"), "$1 $2");
        return NonLetters().Split(separated).Where(word => word.Length > 0).ToArray();
    }

    [GeneratedRegex(@"(\p{Lu}+)(\p{Lu}\p{Ll})")]
    private static partial Regex AcronymBoundary();
    [GeneratedRegex(@"(\p{Ll}|\p{N})(\p{Lu})")]
    private static partial Regex LowerUpperBoundary();
    [GeneratedRegex(@"\P{L}+")]
    private static partial Regex NonLetters();
}
