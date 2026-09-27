using System.Security;

namespace KillerTools.Engine.Documents;

public static class SvgPlaceholder
{
    public static string Build(int width, int height, int fontSize, string background, string foreground, bool exactSize, string customText)
    {
        var text = customText.Length > 0 ? customText : $"{width}x{height}";
        var size = exactSize ? $" width=\"{width}\" height=\"{height}\"" : string.Empty;
        return $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {width} {height}\"{size}>\n  <rect width=\"{width}\" height=\"{height}\" fill=\"{SecurityElement.Escape(background)}\"></rect>\n  <text x=\"50%\" y=\"50%\" dominant-baseline=\"middle\" text-anchor=\"middle\" font-family=\"monospace\" font-size=\"{fontSize}px\" fill=\"{SecurityElement.Escape(foreground)}\">{SecurityElement.Escape(text)}</text>\n</svg>";
    }
}
