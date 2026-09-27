namespace KillerTools.Engine.Text;

public sealed record TextDiffLine(string Kind, string Text);

public sealed record TextDiffResult(bool Equal, int Added, int Removed, IReadOnlyList<TextDiffLine> Lines);

public static class TextDiff
{
    private const int MaximumLines = 200;

    public static TextDiffResult Compare(string left, string right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        var leftLines = left.Split('\n');
        var rightLines = right.Split('\n');
        if (leftLines.Length > MaximumLines || rightLines.Length > MaximumLines)
        {
            throw new ArgumentException($"Each input must contain no more than {MaximumLines} lines.");
        }

        var lengths = new ushort[leftLines.Length + 1, rightLines.Length + 1];
        for (var i = leftLines.Length - 1; i >= 0; i--)
        {
            for (var j = rightLines.Length - 1; j >= 0; j--)
            {
                lengths[i, j] = leftLines[i] == rightLines[j]
                    ? (ushort)(lengths[i + 1, j + 1] + 1)
                    : System.Math.Max(lengths[i + 1, j], lengths[i, j + 1]);
            }
        }

        var lines = new List<TextDiffLine>();
        var leftIndex = 0;
        var rightIndex = 0;
        while (leftIndex < leftLines.Length || rightIndex < rightLines.Length)
        {
            if (leftIndex < leftLines.Length && rightIndex < rightLines.Length && leftLines[leftIndex] == rightLines[rightIndex])
            {
                lines.Add(new TextDiffLine("context", leftLines[leftIndex]));
                leftIndex++;
                rightIndex++;
            }
            else if (rightIndex < rightLines.Length && (leftIndex == leftLines.Length || lengths[leftIndex, rightIndex + 1] >= lengths[leftIndex + 1, rightIndex]))
            {
                lines.Add(new TextDiffLine("add", rightLines[rightIndex++]));
            }
            else
            {
                lines.Add(new TextDiffLine("remove", leftLines[leftIndex++]));
            }
        }

        return new TextDiffResult(
            left == right,
            lines.Count(line => line.Kind == "add"),
            lines.Count(line => line.Kind == "remove"),
            lines);
    }
}
