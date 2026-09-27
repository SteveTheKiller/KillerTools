using System.Globalization;
using System.Text.Json;

namespace KillerTools.Engine.Documents;

public static class JsonCsv
{
    public static string Convert(JsonElement rows)
    {
        var items = rows.EnumerateArray().ToArray();
        var headers = new List<string>();
        foreach (var item in items)
        {
            foreach (var property in item.EnumerateObject())
            {
                if (!headers.Contains(property.Name, StringComparer.Ordinal))
                {
                    headers.Add(property.Name);
                }
            }
        }

        var lines = new List<string> { string.Join(',', headers) };
        lines.AddRange(items.Select(item => string.Join(',', headers.Select(header =>
            item.TryGetProperty(header, out var value) ? Serialize(value) : string.Empty))));
        return string.Join('\n', lines);
    }

    private static string Serialize(JsonElement value)
    {
        var text = value.ValueKind switch
        {
            JsonValueKind.Null => "null",
            JsonValueKind.String => value.GetString()!,
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.Array => string.Join(',', value.EnumerateArray().Select(JavaScriptString)),
            JsonValueKind.Object => "[object Object]",
            _ => string.Empty,
        };
        text = text.Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
        return text.Contains(',', StringComparison.Ordinal) ? $"\"{text}\"" : text;
    }

    private static string JavaScriptString(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null => string.Empty,
        JsonValueKind.String => value.GetString()!,
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Number => value.GetDouble().ToString("G15", CultureInfo.InvariantCulture),
        JsonValueKind.Array => string.Join(',', value.EnumerateArray().Select(JavaScriptString)),
        JsonValueKind.Object => "[object Object]",
        _ => string.Empty,
    };
}
