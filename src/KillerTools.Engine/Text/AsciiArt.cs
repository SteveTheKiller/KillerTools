using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace KillerTools.Engine.Text;

public static partial class AsciiArt
{
    private const int MaximumArtLength = 32_768;
    private const string FontResource = "KillerTools.Engine.Data.figlet-fonts.zip.br64";
    private static readonly Lazy<IReadOnlyDictionary<string, FontData>> Fonts = new(LoadFonts);

    public static string Draw(string text, string fontName = "Standard", int width = 80)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        ArgumentException.ThrowIfNullOrEmpty(fontName);
        if (text.Length > 128)
        {
            throw new ArgumentOutOfRangeException(nameof(text));
        }

        if (fontName.Length > 40 || !FontNamePattern().IsMatch(fontName))
        {
            throw new ArgumentException("The font name is invalid.", nameof(fontName));
        }

        if (width is < 20 or > 200)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (!Fonts.Value.TryGetValue(fontName, out var data))
        {
            throw new ArgumentException("The font is unavailable.", nameof(fontName));
        }

        var font = data.Font.Value;
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var renderedLines = new List<string[]>();
        foreach (var line in normalized.Split('\n'))
        {
            renderedLines.AddRange(RenderWrappedLine(font, line, width));
        }

        var output = renderedLines[0];
        for (var index = 1; index < renderedLines.Count; index++)
        {
            output = SmushVertical(output, renderedLines[index], font.VerticalRules);
        }

        var art = string.Join("\n", output);
        if (art.Length > MaximumArtLength)
        {
            throw new InvalidOperationException("The rendered output is too large.");
        }

        return art;
    }

    public static IReadOnlyList<string> FontNames() => Fonts.Value.Keys.Order(StringComparer.Ordinal).ToArray();

    private static IReadOnlyList<string[]> RenderWrappedLine(FigFont font, string text, int width)
    {
        var output = Empty(font.Height);
        var outputLines = new List<string[]>();
        var nextCharacters = new WordState([], 0);
        var words = new List<FigPiece>();
        if (font.PrintDirection == 1)
        {
            text = new string(text.Reverse().ToArray());
        }

        for (var characterIndex = 0; characterIndex < text.Length; characterIndex++)
        {
            var character = text[characterIndex];
            var isSpace = char.IsWhiteSpace(text[characterIndex]);
            if (!font.Characters.TryGetValue(character, out var glyph))
            {
                continue;
            }

            var overlap = 0;
            if (font.HorizontalRules.Layout != HorizontalLayout.FullWidth)
            {
                overlap = 10_000;
                for (var row = 0; row < font.Height; row++)
                {
                    overlap = System.Math.Min(overlap, HorizontalSmushLength(output[row], glyph[row], font));
                }

                if (overlap == 10_000)
                {
                    overlap = 0;
                }
            }

            var textWord = JoinFigArray(nextCharacters.Characters.Append(new FigPiece(glyph, overlap)), font);
            var textLine = JoinFigArray(words.Append(new FigPiece(textWord, nextCharacters.Overlap)), font);
            var maximumWidth = MaximumWidth(textLine);
            if (maximumWidth >= width && characterIndex > 0)
            {
                output = JoinFigArray(words.Take(System.Math.Max(0, words.Count - 1)), font);
                if (words.Count > 1)
                {
                    outputLines.Add(output);
                    output = Empty(font.Height);
                }

                words.Clear();
            }

            if (!isSpace || characterIndex == text.Length - 1)
            {
                nextCharacters.Characters.Add(new FigPiece(glyph, overlap));
            }

            if (isSpace || characterIndex == text.Length - 1)
            {
                var wordWasBroken = false;
                while (true)
                {
                    textLine = JoinFigArray(nextCharacters.Characters, font);
                    maximumWidth = MaximumWidth(textLine);
                    if (maximumWidth >= width)
                    {
                        var broken = BreakWord(nextCharacters.Characters, font, width);
                        nextCharacters = new WordState(broken.Characters, 0);
                        outputLines.Add(broken.Output);
                        wordWasBroken = true;
                    }
                    else
                    {
                        break;
                    }
                }

                if (maximumWidth > 0)
                {
                    words.Add(new FigPiece(textLine, wordWasBroken ? 1 : nextCharacters.Overlap));
                }

                if (isSpace)
                {
                    words.Add(new FigPiece(glyph, overlap));
                    output = Empty(font.Height);
                }

                if (characterIndex == text.Length - 1)
                {
                    output = JoinFigArray(words, font);
                }

                nextCharacters = new WordState([], overlap);
                continue;
            }

            output = HorizontalSmush(output, glyph, overlap, font);
        }

        if (MaximumWidth(output) > 0)
        {
            outputLines.Add(output);
        }

        foreach (var line in outputLines)
        {
            for (var row = 0; row < line.Length; row++)
            {
                line[row] = line[row].Replace(font.HardBlank, ' ');
            }
        }

        return outputLines.Count == 0 ? [Empty(font.Height)] : outputLines;
    }

    private static BrokenWord BreakWord(List<FigPiece> characters, FigFont font, int width)
    {
        for (var index = characters.Count - 1; index > 0; index--)
        {
            var output = JoinFigArray(characters.Take(index), font);
            if (MaximumWidth(output) <= width)
            {
                return new BrokenWord(output, characters.Skip(index).ToList());
            }
        }

        return characters.Count > 0
            ? new BrokenWord(JoinFigArray(characters.Take(1), font), characters.Skip(1).ToList())
            : new BrokenWord(Empty(font.Height), characters);
    }

    private static string[] JoinFigArray(IEnumerable<FigPiece> pieces, FigFont font)
    {
        var output = Empty(font.Height);
        foreach (var piece in pieces)
        {
            output = HorizontalSmush(output, piece.Fig, piece.Overlap, font);
        }

        return output;
    }

    private static int HorizontalSmushLength(string first, string second, FigFont font)
    {
        var rules = font.HorizontalRules;
        if (rules.Layout == HorizontalLayout.FullWidth || first.Length == 0)
        {
            return 0;
        }

        var maximumDistance = first.Length;
        var currentDistance = 1;
        var breakAfter = false;
        while (currentDistance <= maximumDistance)
        {
            var start = first.Length - currentDistance;
            var firstSegment = first.Substring(start, currentDistance);
            var secondSegment = second[..System.Math.Min(currentDistance, second.Length)];
            for (var index = 0; index < System.Math.Min(currentDistance, second.Length); index++)
            {
                var firstCharacter = firstSegment[index];
                var secondCharacter = secondSegment[index];
                if (firstCharacter == ' ' || secondCharacter == ' ')
                {
                    continue;
                }

                if (rules.Layout == HorizontalLayout.Fitting)
                {
                    currentDistance--;
                    return System.Math.Min(maximumDistance, currentDistance);
                }

                if (rules.Layout == HorizontalLayout.UniversalSmushing)
                {
                    if (firstCharacter == font.HardBlank || secondCharacter == font.HardBlank)
                    {
                        currentDistance--;
                    }

                    return System.Math.Min(maximumDistance, currentDistance);
                }

                breakAfter = true;
                if (ControlledHorizontalSmush(firstCharacter, secondCharacter, font) is null)
                {
                    currentDistance--;
                    return System.Math.Min(maximumDistance, currentDistance);
                }
            }

            if (breakAfter)
            {
                break;
            }

            currentDistance++;
        }

        return System.Math.Min(maximumDistance, currentDistance);
    }

    private static string[] HorizontalSmush(string[] first, string[] second, int overlap, FigFont font)
    {
        var output = new string[font.Height];
        for (var row = 0; row < font.Height; row++)
        {
            var firstLine = first[row];
            var secondLine = second[row];
            var overlapStart = firstLine.Length - overlap;
            var prefix = firstLine[..System.Math.Max(0, overlapStart)];
            var firstSegmentStart = System.Math.Max(0, firstLine.Length - overlap);
            var firstSegment = firstLine.Substring(firstSegmentStart, System.Math.Min(overlap, firstLine.Length - firstSegmentStart));
            var secondSegment = secondLine[..System.Math.Min(overlap, secondLine.Length)];
            var middle = new StringBuilder();
            for (var index = 0; index < overlap; index++)
            {
                var firstCharacter = index < firstLine.Length && index < firstSegment.Length ? firstSegment[index] : ' ';
                var secondCharacter = index < secondLine.Length && index < secondSegment.Length ? secondSegment[index] : ' ';
                if (firstCharacter != ' ' && secondCharacter != ' ')
                {
                    middle.Append(font.HorizontalRules.Layout is HorizontalLayout.Fitting or HorizontalLayout.UniversalSmushing
                        ? UniversalSmush(firstCharacter, secondCharacter, font.HardBlank)
                        : ControlledHorizontalSmush(firstCharacter, secondCharacter, font)
                            ?? UniversalSmush(firstCharacter, secondCharacter, font.HardBlank));
                }
                else
                {
                    middle.Append(UniversalSmush(firstCharacter, secondCharacter, font.HardBlank));
                }
            }

            var suffix = overlap >= secondLine.Length ? string.Empty : secondLine[overlap..];
            output[row] = prefix + middle + suffix;
        }

        return output;
    }

    private static char? ControlledHorizontalSmush(char first, char second, FigFont font)
    {
        var rules = font.HorizontalRules;
        if (rules.Rule1 && first == second && first != font.HardBlank)
        {
            return first;
        }

        if (rules.Rule2)
        {
            const string characters = "|/\\[]{}()<>";
            if (first == '_' && characters.Contains(second, StringComparison.Ordinal))
            {
                return second;
            }

            if (second == '_' && characters.Contains(first, StringComparison.Ordinal))
            {
                return first;
            }
        }

        if (rules.Rule3)
        {
            const string classes = "| /\\ [] {} () <>";
            var firstPosition = classes.IndexOf(first, StringComparison.Ordinal);
            var secondPosition = classes.IndexOf(second, StringComparison.Ordinal);
            if (firstPosition >= 0 && secondPosition >= 0 && firstPosition != secondPosition && System.Math.Abs(firstPosition - secondPosition) != 1)
            {
                return classes[System.Math.Max(firstPosition, secondPosition)];
            }
        }

        if (rules.Rule4)
        {
            const string pairs = "[] {} ()";
            var firstPosition = pairs.IndexOf(first, StringComparison.Ordinal);
            var secondPosition = pairs.IndexOf(second, StringComparison.Ordinal);
            if (firstPosition >= 0 && secondPosition >= 0 && System.Math.Abs(firstPosition - secondPosition) <= 1)
            {
                return '|';
            }
        }

        if (rules.Rule5)
        {
            if (first == '/' && second == '\\') return '|';
            if (first == '\\' && second == '/') return 'Y';
            if (first == '>' && second == '<') return 'X';
        }

        return rules.Rule6 && first == font.HardBlank && second == font.HardBlank ? font.HardBlank : null;
    }

    private static string[] SmushVertical(string[] first, string[] second, VerticalRules rules)
    {
        var firstWidth = first[0].Length;
        var secondWidth = second[0].Length;
        if (firstWidth > secondWidth)
        {
            var padding = new string(' ', firstWidth - secondWidth);
            second = second.Select(line => line + padding).ToArray();
        }
        else if (secondWidth > firstWidth)
        {
            var padding = new string(' ', secondWidth - firstWidth);
            first = first.Select(line => line + padding).ToArray();
        }
        var overlap = VerticalSmushDistance(first, second, rules);
        var result = new List<string>();
        result.AddRange(first.Take(System.Math.Max(0, first.Length - overlap)));

        var overlapLength = System.Math.Min(overlap, first.Length);
        for (var index = 0; index < overlapLength; index++)
        {
            result.Add(SmushVerticalLines(first[first.Length - overlap + index], second[index], rules));
        }

        result.AddRange(second.Skip(System.Math.Min(overlap, second.Length)));
        return result.ToArray();
    }

    private static int VerticalSmushDistance(string[] first, string[] second, VerticalRules rules)
    {
        var maximumDistance = first.Length;
        var currentDistance = 1;
        while (currentDistance <= maximumDistance)
        {
            var firstLines = first.Skip(System.Math.Max(0, first.Length - currentDistance)).ToArray();
            var secondLines = second.Take(System.Math.Min(maximumDistance, currentDistance)).ToArray();
            var result = SmushResult.None;
            for (var index = 0; index < secondLines.Length; index++)
            {
                var current = CanVerticalSmush(firstLines[index], secondLines[index], rules);
                if (current == SmushResult.End)
                {
                    result = current;
                }
                else if (current == SmushResult.Invalid)
                {
                    result = current;
                    break;
                }
                else if (result == SmushResult.None)
                {
                    result = SmushResult.Valid;
                }
            }

            if (result == SmushResult.Invalid)
            {
                currentDistance--;
                break;
            }

            if (result == SmushResult.End)
            {
                break;
            }

            if (result == SmushResult.Valid)
            {
                currentDistance++;
            }
        }

        return System.Math.Min(maximumDistance, currentDistance);
    }

    private static SmushResult CanVerticalSmush(string first, string second, VerticalRules rules)
    {
        if (rules.Layout == VerticalLayout.FullWidth)
        {
            return SmushResult.Invalid;
        }

        var length = System.Math.Min(first.Length, second.Length);
        if (length == 0)
        {
            return SmushResult.Invalid;
        }

        var reachedEnd = false;
        for (var index = 0; index < length; index++)
        {
            var firstCharacter = first[index];
            var secondCharacter = second[index];
            if (firstCharacter == ' ' || secondCharacter == ' ')
            {
                continue;
            }

            if (rules.Layout == VerticalLayout.Fitting)
            {
                return SmushResult.Invalid;
            }

            if (rules.Layout == VerticalLayout.UniversalSmushing)
            {
                return SmushResult.End;
            }

            if (rules.Rule5 && firstCharacter == '|' && secondCharacter == '|')
            {
                continue;
            }

            reachedEnd = true;
            if (ControlledSmush(firstCharacter, secondCharacter, rules) is null)
            {
                return SmushResult.Invalid;
            }
        }

        return reachedEnd ? SmushResult.End : SmushResult.Valid;
    }

    private static string SmushVerticalLines(string first, string second, VerticalRules rules)
    {
        var result = new StringBuilder();
        for (var index = 0; index < System.Math.Min(first.Length, second.Length); index++)
        {
            var firstCharacter = first[index];
            var secondCharacter = second[index];
            if (firstCharacter != ' ' && secondCharacter != ' ')
            {
                result.Append(rules.Layout is VerticalLayout.Fitting or VerticalLayout.UniversalSmushing
                    ? UniversalSmush(firstCharacter, secondCharacter)
                    : ControlledSmush(firstCharacter, secondCharacter, rules));
            }
            else
            {
                result.Append(UniversalSmush(firstCharacter, secondCharacter));
            }
        }

        return result.ToString();
    }

    private static char UniversalSmush(char first, char second, char hardBlank = '\0') =>
        second == ' ' || (second == hardBlank && first != ' ') ? first : second;

    private static char? ControlledSmush(char first, char second, VerticalRules rules)
    {
        if (rules.Rule5 && first == '|' && second == '|')
        {
            return '|';
        }

        if (rules.Rule1 && first == second)
        {
            return first;
        }

        if (rules.Rule2)
        {
            const string characters = "|/\\[]{}()<>";
            if (first == '_' && characters.Contains(second, StringComparison.Ordinal))
            {
                return second;
            }

            if (second == '_' && characters.Contains(first, StringComparison.Ordinal))
            {
                return first;
            }
        }

        if (rules.Rule3)
        {
            const string classes = "| /\\ [] {} () <>";
            var firstPosition = classes.IndexOf(first, StringComparison.Ordinal);
            var secondPosition = classes.IndexOf(second, StringComparison.Ordinal);
            if (firstPosition >= 0 && secondPosition >= 0 && firstPosition != secondPosition && System.Math.Abs(firstPosition - secondPosition) != 1)
            {
                return classes[System.Math.Max(firstPosition, secondPosition)];
            }
        }

        if (rules.Rule4 && ((first == '-' && second == '_') || (first == '_' && second == '-')))
        {
            return '=';
        }

        return null;
    }

    private static int MaximumWidth(string[] lines) => lines.Max(line => line.Length);
    private static string[] Empty(int height) => Enumerable.Repeat(string.Empty, height).ToArray();

    private static IReadOnlyDictionary<string, FontData> LoadFonts()
    {
        using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(FontResource)
            ?? throw new InvalidOperationException("The embedded FIGlet font collection is missing.");
        using var reader = new StreamReader(resource);
        var archiveBytes = Convert.FromBase64String(reader.ReadToEnd());
        using var archiveStream = new MemoryStream(archiveBytes);
        using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read);
        var fonts = new Dictionary<string, FontData>(StringComparer.Ordinal);
        foreach (var entry in archive.Entries)
        {
            using var entryStream = entry.Open();
            using var copy = new MemoryStream();
            entryStream.CopyTo(copy);
            var bytes = copy.ToArray();
            fonts.Add(Path.GetFileNameWithoutExtension(entry.Name), new FontData(bytes));
        }

        return fonts;
    }

    [GeneratedRegex("^[A-Za-z0-9_ '-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex FontNamePattern();

    private sealed class FontData(byte[] bytes)
    {
        public Lazy<FigFont> Font { get; } = new(() => ParseFont(bytes));

        private static FigFont ParseFont(byte[] source)
        {
            var data = Encoding.UTF8.GetString(source).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
            var lines = data.Split('\n').ToList();
            if (lines.Count == 0 || lines[0].Length < 6)
            {
                throw new InvalidDataException("The FIGlet font header is missing.");
            }

            var header = lines[0];
            lines.RemoveAt(0);
            var fields = header.Split(' ');
            var hardBlank = fields[0][5];
            var height = int.Parse(fields[1], System.Globalization.CultureInfo.InvariantCulture);
            var oldLayout = int.Parse(fields[4], System.Globalization.CultureInfo.InvariantCulture);
            var commentLines = int.Parse(fields[5], System.Globalization.CultureInfo.InvariantCulture);
            var printDirection = fields.Length > 6 && fields[6].Length > 0
                ? int.Parse(fields[6], System.Globalization.CultureInfo.InvariantCulture)
                : 0;
            int? fullLayout = fields.Length > 7 && fields[7].Length > 0
                ? int.Parse(fields[7], System.Globalization.CultureInfo.InvariantCulture)
                : null;
            if (height < 1 || commentLines < 0 || lines.Count < commentLines)
            {
                throw new InvalidDataException("The FIGlet font header is invalid.");
            }

            lines.RemoveRange(0, commentLines);
            var characters = new Dictionary<int, string[]>();
            var requiredCharacters = Enumerable.Range(32, 95).Concat([196, 214, 220, 228, 246, 252, 223]).ToArray();
            foreach (var character in requiredCharacters)
            {
                characters[character] = ReadCharacter(lines, height);
            }

            while (lines.Count > 0)
            {
                var codeLine = lines[0];
                lines.RemoveAt(0);
                if (string.IsNullOrWhiteSpace(codeLine))
                {
                    break;
                }

                var token = codeLine.Split(' ')[0];
                var code = ParseCharacterCode(token);
                if (code == -1)
                {
                    throw new InvalidDataException("FIGlet character code -1 is not permitted.");
                }

                characters[code] = ReadCharacter(lines, height);
            }

            var rules = ParseRules(oldLayout, fullLayout);
            return new FigFont(hardBlank, height, printDirection, characters, rules.Horizontal, rules.Vertical);
        }

        private static string[] ReadCharacter(List<string> lines, int height)
        {
            var character = new string[height];
            for (var row = 0; row < height; row++)
            {
                var line = lines.Count > 0 ? lines[0] : string.Empty;
                if (lines.Count > 0)
                {
                    lines.RemoveAt(0);
                }

                var trimmed = line.Trim();
                var endCharacter = trimmed.Length > 0 ? trimmed[^1] : '@';
                var suffix = row == height - 1
                    ? $"{Regex.Escape(endCharacter.ToString())}{Regex.Escape(endCharacter.ToString())}?\\s*$"
                    : $"{Regex.Escape(endCharacter.ToString())}\\s*$";
                character[row] = Regex.Replace(line, suffix, string.Empty);
            }

            return character;
        }

        private static int ParseCharacterCode(string token)
        {
            var negative = token.StartsWith("-", StringComparison.Ordinal);
            var value = negative ? token[1..] : token;
            var radix = 10;
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                radix = 16;
                value = value[2..];
            }
            else if (value.Length > 1 && value[0] == '0')
            {
                radix = 8;
                value = value[1..];
            }

            var parsed = Convert.ToInt32(value, radix);
            return negative ? -parsed : parsed;
        }
    }

    private static (HorizontalRules Horizontal, VerticalRules Vertical) ParseRules(int oldLayout, int? fullLayout)
    {
        var value = fullLayout ?? oldLayout;
        HorizontalLayout? horizontalLayout = null;
        VerticalLayout? verticalLayout = null;
        var horizontal = new bool[6];
        var vertical = new bool[5];
        foreach (var code in new[] { 16384, 8192, 4096, 2048, 1024, 512, 256, 128, 64, 32, 16, 8, 4, 2, 1 })
        {
            var enabled = value >= code;
            if (enabled)
            {
                value -= code;
            }

            switch (code)
            {
                case 16384 when enabled && verticalLayout is null:
                    verticalLayout = VerticalLayout.UniversalSmushing;
                    break;
                case 8192 when enabled && verticalLayout is null:
                    verticalLayout = VerticalLayout.Fitting;
                    break;
                case 4096: vertical[4] = enabled; break;
                case 2048: vertical[3] = enabled; break;
                case 1024: vertical[2] = enabled; break;
                case 512: vertical[1] = enabled; break;
                case 256: vertical[0] = enabled; break;
                case 128 when enabled && horizontalLayout is null:
                    horizontalLayout = HorizontalLayout.UniversalSmushing;
                    break;
                case 64 when enabled && horizontalLayout is null:
                    horizontalLayout = HorizontalLayout.Fitting;
                    break;
                case 32: horizontal[5] = enabled; break;
                case 16: horizontal[4] = enabled; break;
                case 8: horizontal[3] = enabled; break;
                case 4: horizontal[2] = enabled; break;
                case 2: horizontal[1] = enabled; break;
                case 1: horizontal[0] = enabled; break;
            }
        }

        var hasHorizontalRules = horizontal.Any(rule => rule);
        horizontalLayout ??= oldLayout == 0
            ? HorizontalLayout.Fitting
            : oldLayout == -1
                ? HorizontalLayout.FullWidth
                : hasHorizontalRules ? HorizontalLayout.ControlledSmushing : HorizontalLayout.UniversalSmushing;
        if (horizontalLayout == HorizontalLayout.UniversalSmushing && hasHorizontalRules)
        {
            horizontalLayout = HorizontalLayout.ControlledSmushing;
        }

        var hasVerticalRules = vertical.Any(rule => rule);
        verticalLayout ??= hasVerticalRules ? VerticalLayout.ControlledSmushing : VerticalLayout.FullWidth;
        if (verticalLayout == VerticalLayout.UniversalSmushing && hasVerticalRules)
        {
            verticalLayout = VerticalLayout.ControlledSmushing;
        }

        return (
            new HorizontalRules(horizontalLayout.Value, horizontal[0], horizontal[1], horizontal[2], horizontal[3], horizontal[4], horizontal[5]),
            new VerticalRules(verticalLayout.Value, vertical[0], vertical[1], vertical[2], vertical[3], vertical[4]));
    }

    private sealed record FigFont(
        char HardBlank,
        int Height,
        int PrintDirection,
        IReadOnlyDictionary<int, string[]> Characters,
        HorizontalRules HorizontalRules,
        VerticalRules VerticalRules);
    private sealed record FigPiece(string[] Fig, int Overlap);
    private sealed record WordState(List<FigPiece> Characters, int Overlap);
    private sealed record BrokenWord(string[] Output, List<FigPiece> Characters);
    private sealed record HorizontalRules(HorizontalLayout Layout, bool Rule1, bool Rule2, bool Rule3, bool Rule4, bool Rule5, bool Rule6);
    private sealed record VerticalRules(VerticalLayout Layout, bool Rule1, bool Rule2, bool Rule3, bool Rule4, bool Rule5);

    private enum HorizontalLayout
    {
        FullWidth,
        Fitting,
        UniversalSmushing,
        ControlledSmushing,
    }

    private enum VerticalLayout
    {
        FullWidth,
        Fitting,
        ControlledSmushing,
        UniversalSmushing,
    }

    private enum SmushResult
    {
        None,
        Valid,
        End,
        Invalid,
    }
}
