using System.Text.RegularExpressions;

namespace KillerTools.Engine.Text;

public sealed record SafeRegexMatch(int Index, string Match, IReadOnlyList<string?> Groups);
public sealed record SafeRegexResult(IReadOnlyList<SafeRegexMatch> Matches, bool Truncated, string Syntax);

public static class SafeRegex
{
    public static SafeRegexResult Test(string pattern, string text, bool global, bool ignoreCase, bool multiline, bool dotAll)
    {
        var options = RegexOptions.CultureInvariant | RegexOptions.NonBacktracking;
        if (ignoreCase) options |= RegexOptions.IgnoreCase;
        if (multiline) options |= RegexOptions.Multiline;
        if (dotAll) options |= RegexOptions.Singleline;
        var regex = new Regex(pattern, options, TimeSpan.FromSeconds(1));
        var matches = new List<SafeRegexMatch>();
        var truncated = false;
        foreach (Match match in regex.Matches(text))
        {
            if (matches.Count == 50)
            {
                truncated = true;
                break;
            }
            matches.Add(new SafeRegexMatch(match.Index, match.Value, match.Groups.Cast<Group>().Skip(1).Select(group => group.Success ? group.Value : null).ToArray()));
            if (!global) break;
        }
        return new SafeRegexResult(matches, truncated, "RE2");
    }
}
