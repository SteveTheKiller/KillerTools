using Markdig;

namespace KillerTools.Engine.Text;

public static class MarkdownHtml
{
    public static string Convert(string markdown)
    {
        if (markdown.Length > 4096) throw new ArgumentOutOfRangeException(nameof(markdown));
        var html = Markdown.ToHtml(markdown);
        if (html.Length > 16_384) throw new InvalidDataException("Output is too large.");
        return html;
    }
}
