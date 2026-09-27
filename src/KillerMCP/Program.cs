using System.Text;
using System.Text.Json;
using KillerTools.Engine.Text;
using KillerTools.Engine.Math;
using KillerTools.Engine.Numbers;
using KillerTools.Engine.Reference;
using KillerTools.Engine.SystemTools;
using KillerTools.Engine.Documents;
using KillerTools.Engine.Photography;

Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = Encoding.UTF8;
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
        Tool("encode_base64", "Encode text as Base64.", new { text = StringSchema(), urlSafe = BooleanSchema(false) }, ["text"]),
        Tool("decode_base64", "Decode Base64 as text.", new { encoded = StringSchema(), urlSafe = BooleanSchema(false) }, ["encoded"]),
        Tool("text_to_ascii_binary", "Convert text to ASCII binary.", new { text = StringSchema() }, ["text"]),
        Tool("ascii_binary_to_text", "Convert ASCII binary to text.", new { binary = StringSchema() }, ["binary"]),
        Tool("arabic_to_roman", "Convert an integer to Roman numerals.", new { number = IntegerSchema(1, 3999) }, ["number"]),
        Tool("roman_to_arabic", "Convert Roman numerals to an integer.", new { roman = StringSchema() }, ["roman"]),
        Tool("text_to_nato_alphabet", "Convert text to the NATO phonetic alphabet.", new { text = StringSchema() }, ["text"]),
        Tool("convert_integer_base", "Convert an integer between bases.", new { value = StringSchema(), fromBase = IntegerSchema(2, 64), toBase = IntegerSchema(2, 64) }, ["value", "fromBase", "toBase"]),
        Tool("convert_temperature", "Convert a temperature among supported scales.", new { value = NumberSchema(), scale = EnumSchema("kelvin", "celsius", "fahrenheit", "rankine", "delisle", "newton", "reaumur", "romer") }, ["value", "scale"]),
        Tool("calculate_percentage", "Calculate percentages and percentage changes.", new { mode = EnumSchema("percent_of", "what_percent", "change"), x = NumberSchema(), y = NumberSchema() }, ["mode", "x", "y"]),
        Tool("escape_html_entities", "Escape HTML entities.", new { text = StringSchema() }, ["text"]),
        Tool("unescape_html_entities", "Unescape HTML entities.", new { text = StringSchema() }, ["text"]),
        Tool("convert_date_time", "Convert a date among KillerTools formats.", new { value = StringSchema(), inputFormat = EnumSchema("iso8601", "iso9075", "rfc3339", "rfc7231", "unix_seconds", "timestamp_ms", "utc", "mongo_object_id", "excel") }, ["value", "inputFormat"]),
        LookupTool("lookup_exchange_ndr", "Search the Exchange non-delivery report reference."),
        LookupTool("lookup_group_policy", "Search the Group Policy reference."),
        LookupTool("lookup_http_status", "Search the HTTP status code reference."),
        LookupTool("lookup_m365_sku", "Search the Microsoft 365 SKU reference."),
        LookupTool("lookup_port_protocol", "Search the port and protocol reference."),
        LookupTool("lookup_windows_error", "Search the Windows error code reference."),
        LookupTool("lookup_windows_event", "Search the Windows Event ID reference."),
        Tool("diff_text", "Compare two texts line by line.", new { left = StringSchema(), right = StringSchema() }, ["left", "right"]),
        Tool("calculate_nd_exposure", "Calculate exposure time with a neutral density filter.", new { baseSeconds = NumberSchema(), stops = NumberSchema() }, ["baseSeconds", "stops"]),
        Tool("calculate_exposure_equivalence", "Calculate equivalent shutter speed after an aperture change.", new { shutterSeconds = NumberSchema(), originalAperture = NumberSchema(), targetAperture = NumberSchema() }, ["shutterSeconds", "originalAperture", "targetAperture"]),
        Tool("calculate_depth_of_field", "Calculate depth of field.", new { focalLengthMm = NumberSchema(), aperture = NumberSchema(), focusDistance = NumberSchema(), focusUnit = EnumSchema("m", "ft"), circleOfConfusionMm = NumberSchema() }, ["focalLengthMm", "aperture", "focusDistance", "focusUnit", "circleOfConfusionMm"]),
        Tool("generate_svg_placeholder", "Generate an SVG placeholder image.", new { width = IntegerSchema(1, 4096), height = IntegerSchema(1, 4096), fontSize = new { type = "integer", minimum = 1, maximum = 512, @default = 26 }, bgColor = new { type = "string", pattern = "^#[0-9a-f]{6}$", @default = "#cccccc" }, fgColor = new { type = "string", pattern = "^#[0-9a-f]{6}$", @default = "#333333" }, useExactSize = BooleanSchema(true), customText = new { type = "string", maxLength = 256, @default = "" } }, ["width", "height"]),
        Tool("calculate_chmod", "Calculate octal and symbolic Unix permissions.", new { permissions = PermissionSetSchema() }, ["permissions"]),
    ];

    public static object Call(JsonElement parameters, JsonSerializerOptions json)
    {
        var name = parameters.GetProperty("name").GetString();
        var arguments = parameters.GetProperty("arguments");
        object value = name switch
        {
            "text_statistics" => TextStatistics.Calculate(arguments.GetProperty("text").GetString()!),
            "convert_case" => CaseConverter.Convert(arguments.GetProperty("text").GetString()!),
            "encode_base64" => new { encoded = Base64Text.Encode(arguments.GetProperty("text").GetString()!, OptionalBoolean(arguments, "urlSafe")) },
            "decode_base64" => new { text = Base64Text.Decode(arguments.GetProperty("encoded").GetString()!, OptionalBoolean(arguments, "urlSafe")) },
            "text_to_ascii_binary" => new { binary = AsciiBinary.Encode(arguments.GetProperty("text").GetString()!) },
            "ascii_binary_to_text" => new { text = AsciiBinary.Decode(arguments.GetProperty("binary").GetString()!) },
            "arabic_to_roman" => new { roman = RomanNumerals.FromArabic(arguments.GetProperty("number").GetInt32()) },
            "roman_to_arabic" => new { number = RomanNumerals.ToArabic(arguments.GetProperty("roman").GetString()!) },
            "text_to_nato_alphabet" => new { nato = NatoAlphabet.Convert(arguments.GetProperty("text").GetString()!) },
            "convert_integer_base" => new { value = IntegerBases.Convert(arguments.GetProperty("value").GetString()!, arguments.GetProperty("fromBase").GetInt32(), arguments.GetProperty("toBase").GetInt32()) },
            "convert_temperature" => Temperatures.Convert(arguments.GetProperty("value").GetDouble(), arguments.GetProperty("scale").GetString()!),
            "calculate_percentage" => Percentage(arguments),
            "escape_html_entities" => new { text = HtmlEntities.Escape(arguments.GetProperty("text").GetString()!) },
            "unescape_html_entities" => new { text = HtmlEntities.Unescape(arguments.GetProperty("text").GetString()!) },
            "convert_date_time" => DateTimeConverter.Convert(arguments.GetProperty("value").GetString()!, arguments.GetProperty("inputFormat").GetString()!),
            "lookup_exchange_ndr" => MicrosoftReferenceLookups.SearchNdrs(Query(arguments), Limit(arguments)),
            "lookup_group_policy" => GroupPolicyLookup.Search(Query(arguments), Limit(arguments)),
            "lookup_http_status" => HttpStatusLookup.Search(Query(arguments), Limit(arguments)),
            "lookup_m365_sku" => MicrosoftReferenceLookups.SearchSkus(Query(arguments), Limit(arguments)),
            "lookup_port_protocol" => PortProtocolLookup.Search(Query(arguments), Limit(arguments)),
            "lookup_windows_error" => MicrosoftReferenceLookups.SearchErrors(Query(arguments), Limit(arguments)),
            "lookup_windows_event" => MicrosoftReferenceLookups.SearchEvents(Query(arguments), Limit(arguments)),
            "diff_text" => TextDiff.Compare(arguments.GetProperty("left").GetString()!, arguments.GetProperty("right").GetString()!),
            "calculate_nd_exposure" => new { seconds = ExposureCalculators.WithNeutralDensity(arguments.GetProperty("baseSeconds").GetDouble(), arguments.GetProperty("stops").GetDouble()) },
            "calculate_exposure_equivalence" => new { shutterSeconds = ExposureCalculators.EquivalentShutter(arguments.GetProperty("shutterSeconds").GetDouble(), arguments.GetProperty("originalAperture").GetDouble(), arguments.GetProperty("targetAperture").GetDouble()) },
            "calculate_depth_of_field" => ExposureCalculators.DepthOfField(arguments.GetProperty("focalLengthMm").GetDouble(), arguments.GetProperty("aperture").GetDouble(), arguments.GetProperty("focusDistance").GetDouble(), arguments.GetProperty("focusUnit").GetString()!, arguments.GetProperty("circleOfConfusionMm").GetDouble()),
            "generate_svg_placeholder" => Svg(arguments),
            "calculate_chmod" => Chmod(arguments),
            _ => throw new InvalidOperationException($"Unknown tool: {name}"),
        };
        return new { content = new[] { new { type = "text", text = JsonSerializer.Serialize(value, json) } } };
    }

    private static object Tool(string name, string description, object properties, string[] required) =>
        new { name, description, inputSchema = new { type = "object", properties, required, additionalProperties = false } };
    private static object LookupTool(string name, string description) =>
        Tool(name, description, new { query = StringSchema(), limit = new { type = "integer", minimum = 1, maximum = 20, @default = 10 } }, ["query"]);
    private static object StringSchema() => new { type = "string", maxLength = 4096 };
    private static object BooleanSchema(bool defaultValue) => new { type = "boolean", @default = defaultValue };
    private static object IntegerSchema(int minimum, int maximum) => new { type = "integer", minimum, maximum };
    private static object NumberSchema() => new { type = "number" };
    private static object EnumSchema(params string[] values) => new { type = "string", @enum = values };
    private static object PermissionSetSchema()
    {
        var group = new { type = "object", properties = new { read = new { type = "boolean" }, write = new { type = "boolean" }, execute = new { type = "boolean" } }, required = new[] { "read", "write", "execute" }, additionalProperties = false };
        return new { type = "object", properties = new { owner = group, group, @public = group }, required = new[] { "owner", "group", "public" }, additionalProperties = false };
    }
    private static bool OptionalBoolean(JsonElement arguments, string name) => arguments.TryGetProperty(name, out var value) && value.GetBoolean();
    private static string Query(JsonElement arguments) => arguments.GetProperty("query").GetString()!;
    private static int Limit(JsonElement arguments) => arguments.TryGetProperty("limit", out var value) ? value.GetInt32() : 10;
    private static object Percentage(JsonElement arguments)
    {
        var x = arguments.GetProperty("x").GetDouble();
        var y = arguments.GetProperty("y").GetDouble();
        var value = arguments.GetProperty("mode").GetString() switch
        {
            "percent_of" => Percentages.Of(x, y),
            "what_percent" => Percentages.Ratio(x, y),
            "change" => Percentages.Change(x, y),
            _ => throw new InvalidOperationException("Unknown percentage mode."),
        };
        return new { value };
    }
    private static object Svg(JsonElement arguments)
    {
        var svg = SvgPlaceholder.Build(arguments.GetProperty("width").GetInt32(), arguments.GetProperty("height").GetInt32(),
            arguments.TryGetProperty("fontSize", out var fontSize) ? fontSize.GetInt32() : 26,
            arguments.TryGetProperty("bgColor", out var background) ? background.GetString()! : "#cccccc",
            arguments.TryGetProperty("fgColor", out var foreground) ? foreground.GetString()! : "#333333",
            !arguments.TryGetProperty("useExactSize", out var exact) || exact.GetBoolean(),
            arguments.TryGetProperty("customText", out var text) ? text.GetString()! : "");
        return new { svg, dataUrl = $"data:image/svg+xml;base64,{Base64Text.Encode(svg)}" };
    }
    private static object Chmod(JsonElement arguments)
    {
        var permissions = arguments.GetProperty("permissions");
        PermissionGroup Group(string name)
        {
            var group = permissions.GetProperty(name);
            return new(group.GetProperty("read").GetBoolean(), group.GetProperty("write").GetBoolean(), group.GetProperty("execute").GetBoolean());
        }
        return UnixPermissions.Calculate(new(Group("owner"), Group("group"), Group("public")));
    }
}
