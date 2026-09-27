using System.Text;
using System.Xml.Linq;

namespace KillerTools.Engine.Documents;

public static class XmlFormatter
{
    public static string Format(string text, int indentSize = 2, bool collapseContent = true)
    {
        if (indentSize is < 0 or > 10) throw new ArgumentOutOfRangeException(nameof(indentSize));
        var document = XDocument.Parse(text.Trim(), LoadOptions.PreserveWhitespace);
        var output = new StringBuilder();
        if (document.Declaration is not null)
        {
            output.Append("<?xml version=\"").Append(document.Declaration.Version).Append('"');
            if (document.Declaration.Encoding is not null) output.Append(" encoding=\"").Append(document.Declaration.Encoding).Append('"');
            if (document.Declaration.Standalone is not null) output.Append(" standalone=\"").Append(document.Declaration.Standalone).Append('"');
            output.Append("?>\n");
        }
        foreach (var node in document.Nodes())
        {
            if (node is XElement element) WriteElement(output, element, 0, indentSize, collapseContent);
            else if (node is XComment comment) output.Append("<!--").Append(comment.Value).Append("-->\n");
        }
        return output.ToString().TrimEnd('\n');
    }

    private static void WriteElement(StringBuilder output, XElement element, int depth, int indentSize, bool collapseContent)
    {
        var indent = new string(' ', depth * indentSize);
        output.Append(indent).Append('<').Append(Name(element));
        WriteAttributes(output, element);
        var nodes = element.Nodes().Where(node => node is not XText text || !string.IsNullOrWhiteSpace(text.Value)).ToArray();
        if (nodes.Length == 0)
        {
            output.Append("/>\n");
            return;
        }
        if (collapseContent && nodes.Any(node => node is not XElement))
        {
            output.Append('>');
            foreach (var node in nodes) WriteCompact(output, node);
            output.Append("</").Append(Name(element)).Append(">\n");
            return;
        }
        output.Append(">\n");
        foreach (var node in nodes)
        {
            if (node is XElement child) WriteElement(output, child, depth + 1, indentSize, collapseContent);
            else WriteExpanded(output, node, depth + 1, indentSize);
        }
        output.Append(indent).Append("</").Append(Name(element)).Append(">\n");
    }

    private static void WriteCompact(StringBuilder output, XNode node)
    {
        switch (node)
        {
            case XElement element:
                output.Append('<').Append(Name(element));
                WriteAttributes(output, element);
                var children = element.Nodes().Where(child => child is not XText text || !string.IsNullOrWhiteSpace(text.Value)).ToArray();
                if (children.Length == 0) output.Append("/>");
                else
                {
                    output.Append('>');
                    foreach (var child in children) WriteCompact(output, child);
                    output.Append("</").Append(Name(element)).Append('>');
                }
                break;
            case XCData cdata:
                output.Append("<![CDATA[").Append(cdata.Value).Append("]]>");
                break;
            case XComment comment:
                output.Append("<!--").Append(comment.Value).Append("-->");
                break;
            case XText text:
                output.Append(EscapeText(text.Value));
                break;
        }
    }

    private static void WriteExpanded(StringBuilder output, XNode node, int depth, int indentSize)
    {
        var indent = new string(' ', depth * indentSize);
        if (node is XText text) output.Append(indent).Append(EscapeText(text.Value.Trim())).Append('\n');
        else if (node is XCData cdata) output.Append(indent).Append("<![CDATA[").Append(cdata.Value).Append("]]>\n");
        else if (node is XComment comment) output.Append(indent).Append("<!--").Append(comment.Value).Append("-->\n");
    }

    private static void WriteAttributes(StringBuilder output, XElement element)
    {
        foreach (var attribute in element.Attributes()) output.Append(' ').Append(Name(attribute)).Append("=\"").Append(EscapeAttribute(attribute.Value)).Append('"');
    }

    private static string Name(XElement element)
    {
        var prefix = element.GetPrefixOfNamespace(element.Name.Namespace);
        return string.IsNullOrEmpty(prefix) ? element.Name.LocalName : $"{prefix}:{element.Name.LocalName}";
    }

    private static string Name(XAttribute attribute)
    {
        if (attribute.IsNamespaceDeclaration) return attribute.Name.LocalName == "xmlns" ? "xmlns" : $"xmlns:{attribute.Name.LocalName}";
        var prefix = attribute.Parent?.GetPrefixOfNamespace(attribute.Name.Namespace);
        return string.IsNullOrEmpty(prefix) ? attribute.Name.LocalName : $"{prefix}:{attribute.Name.LocalName}";
    }

    private static string EscapeText(string value) => value.Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal);
    private static string EscapeAttribute(string value) => EscapeText(value).Replace("\"", "&quot;", StringComparison.Ordinal);
}
