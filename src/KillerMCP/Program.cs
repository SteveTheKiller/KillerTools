using System.Text.Json;
using KillerTools.Engine.Text;

var json = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
string? line;
while ((line = Console.ReadLine()) is not null)
{
    JsonElement request;
    try { request = JsonSerializer.Deserialize<JsonElement>(line); }
    catch { continue; }
    if (!request.TryGetProperty("id", out var id) || !request.TryGetProperty("method", out var methodElement))
    {
        continue;
    }
    var method = methodElement.GetString();
    object result = method switch
    {
        "initialize" => new
        {
            protocolVersion = "2025-06-18",
            capabilities = new { tools = new { listChanged = false } },
            serverInfo = new { name = "KillerMCP", version = "0.1.2" },
            instructions = "Use KillerMCP tools by task. Exact tool names are optional when the request is clear.",
        },
        "tools/list" => new { tools = ToolRegistry.Tools },
        "tools/call" => ToolRegistry.Call(request.GetProperty("params"), json),
        _ => throw new InvalidOperationException($"Unknown MCP method: {method}"),
    };
    Console.WriteLine(JsonSerializer.Serialize(new { jsonrpc = "2.0", id, result }, json));
}

static class ToolRegistry
{
    public static readonly object[] Tools =
    [
        Tool("text_statistics", "Count characters, words, lines, and UTF-8 bytes.", new { text = StringSchema() }, ["text"]),
        Tool("convert_case", "Return the case conversions shown by KillerTools.", new { text = StringSchema() }, ["text"]),
    ];

    public static object Call(JsonElement parameters, JsonSerializerOptions json)
    {
        var name = parameters.GetProperty("name").GetString();
        var arguments = parameters.GetProperty("arguments");
        object value = name switch
        {
            "text_statistics" => TextStatistics.Calculate(arguments.GetProperty("text").GetString()!),
            "convert_case" => CaseConverter.Convert(arguments.GetProperty("text").GetString()!),
            _ => throw new InvalidOperationException($"Unknown tool: {name}"),
        };
        return new { content = new[] { new { type = "text", text = JsonSerializer.Serialize(value, json) } } };
    }

    private static object Tool(string name, string description, object properties, string[] required) =>
        new { name, description, inputSchema = new { type = "object", properties, required, additionalProperties = false } };
    private static object StringSchema() => new { type = "string", maxLength = 4096 };
}
