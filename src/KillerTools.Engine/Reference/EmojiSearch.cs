using System.Text.Json.Serialization;

namespace KillerTools.Engine.Reference;

public sealed record EmojiEntry(
    [property: JsonPropertyName("emoji")] string Emoji,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("group")] string Group,
    [property: JsonPropertyName("keywords")] IReadOnlyList<string> Keywords,
    [property: JsonPropertyName("codePoint")] string CodePoint);

public static class EmojiSearch
{
    private const double Threshold = 0.3;
    private const int Distance = 100;
    private const double NumberEpsilon = 2.2204460492503131e-16;
    private static readonly Lazy<IReadOnlyList<EmojiEntry>> Entries = new(LoadEntries);
    private static readonly KeyDefinition[] Keys =
    [
        new(entry => [entry.Group], 1d / 7d),
        new(entry => [entry.Name], 3d / 7d),
        new(entry => entry.Keywords, 1d / 7d),
        new(entry => [entry.CodePoint], 1d / 7d),
        new(entry => [entry.Emoji], 1d / 7d),
    ];

    public static IReadOnlyList<EmojiEntry> Search(string query, int limit = 10)
    {
        query = query.Trim();
        if (query.Length is < 1 or > 80) throw new ArgumentOutOfRangeException(nameof(query));
        if (limit is < 1 or > 30) throw new ArgumentOutOfRangeException(nameof(limit));

        var searcher = ExtendedSearcher.Create(query);
        var results = new List<SearchResult>();
        for (var index = 0; index < Entries.Value.Count; index++)
        {
            var item = Entries.Value[index];
            var matches = new List<FieldMatch>();
            var anyKeyFailed = false;
            var hasInverse = false;
            foreach (var key in Keys)
            {
                var keyMatches = new List<FieldMatch>();
                foreach (var value in key.Values(item))
                {
                    if (string.IsNullOrWhiteSpace(value)) continue;
                    var match = searcher.Search(value);
                    if (match.IsMatch) keyMatches.Add(new(match.Score, key.Weight, FieldNorm(value), match.HasInverse));
                }
                if (keyMatches.Count == 0)
                {
                    anyKeyFailed = true;
                    continue;
                }
                matches.AddRange(keyMatches);
                if (keyMatches[0].HasInverse) hasInverse = true;
            }
            if (hasInverse && anyKeyFailed || matches.Count == 0) continue;

            var score = 1d;
            foreach (var match in matches)
            {
                var value = match.Score == 0 ? NumberEpsilon : match.Score;
                score *= System.Math.Pow(value, match.Weight * match.Norm);
            }
            results.Add(new(index, score, item));
        }

        return results.OrderBy(result => result.Score).ThenBy(result => result.Index).Take(limit).Select(result => result.Item).ToArray();
    }

    private static IReadOnlyList<EmojiEntry> LoadEntries() => EmbeddedCatalog.DeserializeBrotliBase64<List<EmojiEntry>>("KillerTools.Engine.Data.emoji-catalog.br64");

    private static double FieldNorm(string value)
    {
        var words = 0;
        var inWord = false;
        foreach (var character in value)
        {
            var separator = character is >= '\t' and <= '\r' or ' ' or '\u00a0';
            if (!separator && !inWord)
            {
                words++;
                inWord = true;
            }
            else if (separator)
            {
                inWord = false;
            }
        }
        return System.Math.Floor(1000d / System.Math.Sqrt(System.Math.Max(words, 1)) + 0.5d) / 1000d;
    }

    private sealed record KeyDefinition(Func<EmojiEntry, IReadOnlyList<string>> Values, double Weight);
    private sealed record FieldMatch(double Score, double Weight, double Norm, bool HasInverse);
    private sealed record SearchResult(int Index, double Score, EmojiEntry Item);
    private readonly record struct MatchResult(bool IsMatch, double Score, bool HasInverse = false);

    private enum MatcherType
    {
        Exact,
        Include,
        PrefixExact,
        InversePrefixExact,
        InverseSuffixExact,
        SuffixExact,
        InverseExact,
        Fuzzy,
    }

    private sealed record Matcher(MatcherType Type, string Pattern)
    {
        public MatchResult Search(string text)
        {
            var matched = Type switch
            {
                MatcherType.Exact => text == Pattern,
                MatcherType.Include => text.Contains(Pattern, StringComparison.Ordinal),
                MatcherType.PrefixExact => text.StartsWith(Pattern, StringComparison.Ordinal),
                MatcherType.InversePrefixExact => !text.StartsWith(Pattern, StringComparison.Ordinal),
                MatcherType.InverseSuffixExact => !text.EndsWith(Pattern, StringComparison.Ordinal),
                MatcherType.SuffixExact => text.EndsWith(Pattern, StringComparison.Ordinal),
                MatcherType.InverseExact => !text.Contains(Pattern, StringComparison.Ordinal),
                _ => false,
            };
            if (Type == MatcherType.Fuzzy) return Bitap(Pattern, text);
            var inverse = Type is MatcherType.InversePrefixExact or MatcherType.InverseSuffixExact or MatcherType.InverseExact;
            return new(matched, matched ? 0 : 1, inverse && matched);
        }
    }

    private sealed class ExtendedSearcher
    {
        private readonly IReadOnlyList<IReadOnlyList<Matcher>> _groups;

        private ExtendedSearcher(IReadOnlyList<IReadOnlyList<Matcher>> groups) => _groups = groups;

        public static ExtendedSearcher Create(string pattern)
        {
            pattern = pattern.ToLowerInvariant();
            var groups = SplitOr(pattern).Select(group => Tokenize(group.Trim()).Select(CreateMatcher).ToArray()).ToArray();
            return new(groups);
        }

        public MatchResult Search(string value)
        {
            var text = value.ToLowerInvariant();
            foreach (var group in _groups)
            {
                var totalScore = 0d;
                var matches = 0;
                var inverse = false;
                foreach (var matcher in group)
                {
                    var result = matcher.Search(text);
                    if (!result.IsMatch)
                    {
                        matches = 0;
                        break;
                    }
                    matches++;
                    totalScore += result.Score;
                    inverse |= result.HasInverse;
                }
                if (matches > 0) return new(true, totalScore / matches, inverse);
            }
            return new(false, 1);
        }

        private static IReadOnlyList<string> SplitOr(string pattern)
        {
            const char escapedPipe = '\0';
            return pattern.Replace("\\|", escapedPipe.ToString(), StringComparison.Ordinal).Split('|').Select(item => item.Replace(escapedPipe, '|')).ToArray();
        }

        private static IReadOnlyList<string> Tokenize(string pattern)
        {
            var tokens = new List<string>();
            var index = 0;
            while (index < pattern.Length)
            {
                while (index < pattern.Length && pattern[index] == ' ') index++;
                if (index >= pattern.Length) break;
                var end = index;
                while (end < pattern.Length && pattern[end] != ' ' && pattern[end] != '"') end++;
                if (end < pattern.Length && pattern[end] == '"')
                {
                    end++;
                    while (end < pattern.Length)
                    {
                        if (pattern[end] == '"')
                        {
                            var next = end + 1;
                            if (next >= pattern.Length || pattern[next] == ' ')
                            {
                                end++;
                                break;
                            }
                            if (pattern[next] == '$' && (next + 1 >= pattern.Length || pattern[next + 1] == ' '))
                            {
                                end += 2;
                                break;
                            }
                        }
                        end++;
                    }
                }
                else
                {
                    while (end < pattern.Length && pattern[end] != ' ') end++;
                }
                tokens.Add(pattern[index..end]);
                index = end;
            }
            return tokens.Where(token => !string.IsNullOrWhiteSpace(token)).ToArray();
        }

        private static Matcher CreateMatcher(string token)
        {
            if (token.StartsWith("=\"", StringComparison.Ordinal) && token.EndsWith('"')) return new(MatcherType.Exact, token[2..^1]);
            if (token.StartsWith('=') && token.Length > 1) return new(MatcherType.Exact, token[1..]);
            if (token.StartsWith("'\"", StringComparison.Ordinal) && token.EndsWith('"')) return new(MatcherType.Include, token[2..^1]);
            if (token.StartsWith('\'') && token.Length > 1) return new(MatcherType.Include, token[1..]);
            if (token.StartsWith("^\"", StringComparison.Ordinal) && token.EndsWith('"')) return new(MatcherType.PrefixExact, token[2..^1]);
            if (token.StartsWith('^') && token.Length > 1) return new(MatcherType.PrefixExact, token[1..]);
            if (token.StartsWith("!^\"", StringComparison.Ordinal) && token.EndsWith('"')) return new(MatcherType.InversePrefixExact, token[3..^1]);
            if (token.StartsWith("!^", StringComparison.Ordinal) && token.Length > 2) return new(MatcherType.InversePrefixExact, token[2..]);
            if (token.StartsWith("!\"", StringComparison.Ordinal) && token.EndsWith("\"$", StringComparison.Ordinal)) return new(MatcherType.InverseSuffixExact, token[2..^2]);
            if (token.StartsWith('!') && token.EndsWith('$') && token.Length > 2) return new(MatcherType.InverseSuffixExact, token[1..^1]);
            if (token.StartsWith('"') && token.EndsWith("\"$", StringComparison.Ordinal)) return new(MatcherType.SuffixExact, token[1..^2]);
            if (token.EndsWith('$') && token.Length > 1) return new(MatcherType.SuffixExact, token[..^1]);
            if (token.StartsWith("!\"", StringComparison.Ordinal) && token.EndsWith('"')) return new(MatcherType.InverseExact, token[2..^1]);
            if (token.StartsWith('!') && token.Length > 1) return new(MatcherType.InverseExact, token[1..]);
            if (token.StartsWith('"') && token.EndsWith('"') && token.Length > 1) return new(MatcherType.Fuzzy, token[1..^1]);
            return new(MatcherType.Fuzzy, token);
        }
    }

    private static MatchResult Bitap(string pattern, string text)
    {
        if (pattern == text) return new(true, 0);
        var chunks = new List<(string Pattern, int Start)>();
        if (pattern.Length > 32)
        {
            var remainder = pattern.Length % 32;
            var end = pattern.Length - remainder;
            for (var index = 0; index < end; index += 32) chunks.Add((pattern.Substring(index, 32), index));
            if (remainder != 0) chunks.Add((pattern[^32..], pattern.Length - 32));
        }
        else
        {
            chunks.Add((pattern, 0));
        }

        var hasMatch = false;
        var score = 0d;
        foreach (var chunk in chunks)
        {
            var result = BitapChunk(text, chunk.Pattern, chunk.Start);
            hasMatch |= result.IsMatch;
            score += result.Score;
        }
        return new(hasMatch, hasMatch ? score / chunks.Count : 1);
    }

    private static MatchResult BitapChunk(string text, string pattern, int location)
    {
        var alphabet = new Dictionary<char, int>();
        for (var index = 0; index < pattern.Length; index++)
        {
            alphabet.TryGetValue(pattern[index], out var bits);
            alphabet[pattern[index]] = bits | 1 << pattern.Length - index - 1;
        }

        var expectedLocation = System.Math.Max(0, System.Math.Min(location, text.Length));
        var currentThreshold = Threshold;
        var bestLocation = expectedLocation;
        double Score(int errors, int currentLocation)
        {
            var accuracy = (double)errors / pattern.Length;
            var proximity = System.Math.Abs(expectedLocation - currentLocation);
            return accuracy + (double)proximity / Distance;
        }

        var found = text.IndexOf(pattern, bestLocation, StringComparison.Ordinal);
        while (found >= 0)
        {
            currentThreshold = System.Math.Min(Score(0, found), currentThreshold);
            bestLocation = found + pattern.Length;
            found = text.IndexOf(pattern, bestLocation, StringComparison.Ordinal);
        }

        bestLocation = -1;
        var previous = Array.Empty<int>();
        var finalScore = 1d;
        var maximum = pattern.Length + text.Length;
        var mask = 1 << pattern.Length - 1;
        for (var errors = 0; errors < pattern.Length; errors++)
        {
            var minimum = 0;
            var middle = maximum;
            while (minimum < middle)
            {
                if (Score(errors, expectedLocation + middle) <= currentThreshold) minimum = middle;
                else maximum = middle;
                middle = (maximum - minimum) / 2 + minimum;
            }
            maximum = middle;
            var start = System.Math.Max(1, expectedLocation - middle + 1);
            var finish = System.Math.Min(expectedLocation + middle, text.Length) + pattern.Length;
            var bits = new int[finish + 2];
            bits[finish + 1] = (1 << errors) - 1;
            for (var cursor = finish; cursor >= start; cursor--)
            {
                var currentLocation = cursor - 1;
                var characterBits = currentLocation < text.Length && alphabet.TryGetValue(text[currentLocation], out var value) ? value : 0;
                bits[cursor] = unchecked(((bits[cursor + 1] << 1) | 1) & characterBits);
                if (errors != 0)
                {
                    var previousNext = cursor + 1 < previous.Length ? previous[cursor + 1] : 0;
                    var previousCurrent = cursor < previous.Length ? previous[cursor] : 0;
                    bits[cursor] |= unchecked(((previousNext | previousCurrent) << 1) | 1 | previousNext);
                }
                if ((bits[cursor] & mask) == 0) continue;
                finalScore = Score(errors, currentLocation);
                if (finalScore > currentThreshold) continue;
                currentThreshold = finalScore;
                bestLocation = currentLocation;
                if (bestLocation <= expectedLocation) break;
                start = System.Math.Max(1, 2 * expectedLocation - bestLocation);
            }
            if (Score(errors + 1, expectedLocation) > currentThreshold) break;
            previous = bits;
        }
        return new(bestLocation >= 0, System.Math.Max(0.001, finalScore));
    }
}
