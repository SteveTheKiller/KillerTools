using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Xml.Linq;

namespace KillerTools.Engine.Documents;

public static class XmlJsonConverter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        IndentCharacter = ' ',
        IndentSize = 2,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string XmlToJson(string text)
    {
        var document = XDocument.Parse(text, LoadOptions.PreserveWhitespace);
        var result = new Dictionary<string, object?>();
        if (document.Declaration is not null)
        {
            var attributes = new Dictionary<string, object?> { ["version"] = document.Declaration.Version };
            if (document.Declaration.Encoding is not null) attributes["encoding"] = document.Declaration.Encoding;
            if (document.Declaration.Standalone is not null) attributes["standalone"] = document.Declaration.Standalone;
            result["_declaration"] = new Dictionary<string, object?> { ["_attributes"] = attributes };
        }
        foreach (var node in document.Nodes())
        {
            if (node is XElement element) Add(result, QualifiedName(element), ConvertElement(element));
            else if (node is XComment comment) Add(result, "_comment", comment.Value);
        }
        return JsonSerializer.Serialize(result, JsonOptions).Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    public static string JsonToXml(string text)
    {
        var value = StructuredJson.Parse(text) as Dictionary<string, object?> ?? throw new FormatException("The XML JSON root must be an object.");
        var output = new StringBuilder();
        foreach (var entry in value)
        {
            if (entry.Key == "_declaration") WriteDeclaration(output, entry.Value);
            else WriteValues(output, entry.Key, entry.Value, 0);
        }
        return output.ToString().TrimEnd('\n');
    }

    private static Dictionary<string, object?> ConvertElement(XElement element)
    {
        var result = new Dictionary<string, object?>();
        if (element.HasAttributes)
        {
            result["_attributes"] = element.Attributes().ToDictionary(QualifiedName, attribute => (object?)attribute.Value, StringComparer.Ordinal);
        }
        foreach (var node in element.Nodes())
        {
            switch (node)
            {
                case XElement child:
                    Add(result, QualifiedName(child), ConvertElement(child));
                    break;
                case XCData cdata:
                    Add(result, "_cdata", cdata.Value);
                    break;
                case XText text when !string.IsNullOrWhiteSpace(text.Value):
                    Add(result, "_text", text.Value);
                    break;
                case XComment comment:
                    Add(result, "_comment", comment.Value);
                    break;
            }
        }
        return result;
    }

    private static void Add(Dictionary<string, object?> target, string key, object? value)
    {
        if (!target.TryGetValue(key, out var current)) target[key] = value;
        else if (current is List<object?> list) list.Add(value);
        else target[key] = new List<object?> { current, value };
    }

    private static string QualifiedName(XElement element)
    {
        var prefix = element.GetPrefixOfNamespace(element.Name.Namespace);
        return string.IsNullOrEmpty(prefix) ? element.Name.LocalName : $"{prefix}:{element.Name.LocalName}";
    }

    private static string QualifiedName(XAttribute attribute)
    {
        if (attribute.IsNamespaceDeclaration) return attribute.Name.LocalName == "xmlns" ? "xmlns" : $"xmlns:{attribute.Name.LocalName}";
        var prefix = attribute.Parent?.GetPrefixOfNamespace(attribute.Name.Namespace);
        return string.IsNullOrEmpty(prefix) ? attribute.Name.LocalName : $"{prefix}:{attribute.Name.LocalName}";
    }

    private static void WriteDeclaration(StringBuilder output, object? value)
    {
        var declaration = value as Dictionary<string, object?> ?? throw new FormatException("Invalid XML declaration.");
        var attributes = declaration.GetValueOrDefault("_attributes") as Dictionary<string, object?> ?? [];
        output.Append("<?xml");
        foreach (var attribute in attributes) output.Append(' ').Append(attribute.Key).Append("=\"").Append(EscapeAttribute(Scalar(attribute.Value))).Append('"');
        output.Append("?>\n");
    }

    private static void WriteValues(StringBuilder output, string name, object? value, int depth)
    {
        if (value is List<object?> list)
        {
            foreach (var item in list) WriteValues(output, name, item, depth);
            return;
        }
        WriteElement(output, name, value as Dictionary<string, object?> ?? new Dictionary<string, object?> { ["_text"] = value }, depth);
    }

    private static void WriteElement(StringBuilder output, string name, Dictionary<string, object?> value, int depth)
    {
        var indent = new string(' ', depth * 2);
        output.Append(indent).Append('<').Append(name);
        if (value.GetValueOrDefault("_attributes") is Dictionary<string, object?> attributes)
        {
            foreach (var attribute in attributes) output.Append(' ').Append(attribute.Key).Append("=\"").Append(EscapeAttribute(Scalar(attribute.Value))).Append('"');
        }
        var content = value.Where(item => item.Key != "_attributes").ToArray();
        if (content.Length == 0)
        {
            output.Append("/>\n");
            return;
        }
        if (content.Length == 1 && content[0].Key == "_text" && content[0].Value is not List<object?>)
        {
            output.Append('>').Append(EscapeText(Scalar(content[0].Value))).Append("</").Append(name).Append(">\n");
            return;
        }
        output.Append(">\n");
        foreach (var item in content)
        {
            if (item.Key == "_text") WriteSpecial(output, ScalarValues(item.Value).Select(EscapeText), depth + 1);
            else if (item.Key == "_comment") WriteSpecial(output, ScalarValues(item.Value).Select(text => $"<!--{text}-->"), depth + 1);
            else if (item.Key == "_cdata") WriteSpecial(output, ScalarValues(item.Value).Select(text => $"<![CDATA[{text}]]>"), depth + 1);
            else WriteValues(output, item.Key, item.Value, depth + 1);
        }
        output.Append(indent).Append("</").Append(name).Append(">\n");
    }

    private static void WriteSpecial(StringBuilder output, IEnumerable<string> values, int depth)
    {
        var indent = new string(' ', depth * 2);
        foreach (var value in values) output.Append(indent).Append(value).Append('\n');
    }

    private static IEnumerable<string> ScalarValues(object? value) => value is List<object?> list ? list.Select(Scalar) : [Scalar(value)];
    private static string Scalar(object? value) => value switch
    {
        null => string.Empty,
        bool boolean => boolean ? "true" : "false",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };
    private static string EscapeText(string value) => value.Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal);
    private static string EscapeAttribute(string value) => EscapeText(value).Replace("\"", "&quot;", StringComparison.Ordinal);
}
