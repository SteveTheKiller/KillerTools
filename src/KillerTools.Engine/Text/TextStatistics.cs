using System.Text;
using System.Text.RegularExpressions;

namespace KillerTools.Engine.Text;

public readonly record struct TextStatisticsResult(
    int CharacterCount,
    int WordCount,
    int LineCount,
    int ByteSize);

public static partial class TextStatistics
{
    public static TextStatisticsResult Calculate(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return new TextStatisticsResult(
            text.Length,
            text.Length == 0 ? 0 : Whitespace().Split(text).Length,
            text.Length == 0 ? 0 : LineBreak().Split(text).Length,
            Encoding.UTF8.GetByteCount(text));
    }

    [GeneratedRegex("\\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex("\\r\\n|\\r|\\n")]
    private static partial Regex LineBreak();
}

