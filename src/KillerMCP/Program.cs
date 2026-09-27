using System.Text;
using System.Text.Json;
using System.Reflection;
using KillerMCP;
using KillerMCP.Runtime;
using KillerTools.Engine.Text;
using KillerTools.Engine.Math;
using KillerTools.Engine.Numbers;
using KillerTools.Engine.Reference;
using KillerTools.Engine.SystemTools;
using KillerTools.Engine.Documents;
using KillerTools.Engine.Photography;
using KillerTools.Engine.Network;
using KillerTools.Engine.Security;

Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = Encoding.UTF8;
var json = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version!;
var currentVersion = $"{assemblyVersion.Major}.{assemblyVersion.Minor}.{assemblyVersion.Build}";
ToolRegistry.Status = await UpdateChecker.CheckForUpdateAsync(currentVersion);
var appAdapters = new List<AppAdapter>();
var killerNotes = await KillerNotesAdapter.CreateAsync(AppDiscovery.Discover("killernotes"));
if (killerNotes is not null)
{
    appAdapters.Add(killerNotes);
}
var killendar = await KillendarAdapter.CreateAsync(AppDiscovery.Discover("killendar"));
if (killendar is not null)
{
    appAdapters.Add(killendar);
}
var killerShell = await KillerShellAdapter.CreateAsync(AppDiscovery.Discover("killershell"));
if (killerShell is not null)
{
    appAdapters.Add(killerShell);
}
appAdapters.AddRange(KillerBenchAdapter.Create(AppDiscovery.Discover("killerbench")));
appAdapters.AddRange(await KillerScanAdapter.CreateAsync(AppDiscovery.Discover("killerscan")));
appAdapters.AddRange(await KillerPdfAdapter.CreateAsync(AppDiscovery.Discover("killerpdf")));
ToolRegistry.SetAdapters(appAdapters);
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
            serverInfo = new { name = "KillerMCP", version = currentVersion },
            instructions = "Use KillerMCP tools by task. Exact tool names are optional when the request is clear." + UpdateChecker.Instruction(ToolRegistry.Status),
        },
        "tools/list" => new { tools = ToolRegistry.Tools },
        "tools/call" => ToolRegistry.Call(request.GetProperty("params"), json),
        _ => throw new InvalidOperationException($"Unknown MCP method: {method}"),
    };
    Console.WriteLine(JsonSerializer.Serialize(new { jsonrpc = "2.0", id, result }, json));
}

static class ToolRegistry
{
    private static readonly JsonSerializerOptions CamelCaseJson = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static IReadOnlyDictionary<string, AppAdapter> Adapters = new Dictionary<string, AppAdapter>();
    public static UpdateStatus Status { get; set; } = null!;

    private static readonly object[] CoreTools =
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
        Tool("list_killer_modules", "List the KillerTools PowerShell modules, commands, install text, and repository links.", new { name = new { type = "string", maxLength = 80 } }, []),
        Tool("list_killer_scripts", "List KillerScripts names, descriptions, and download links from GitHub.", new { query = new { type = "string", maxLength = 80, @default = "" } }, []),
        Tool("diff_text", "Compare two texts line by line.", new { left = StringSchema(), right = StringSchema() }, ["left", "right"]),
        Tool("format_json", "Prettify JSON5-compatible text with optional key sorting.", new { text = StructuredTextSchema(), indentSize = new { type = "integer", minimum = 0, maximum = 10, @default = 3 }, sortKeys = BooleanSchema(true) }, ["text"]),
        Tool("minify_json", "Minify JSON5-compatible text.", new { text = StructuredTextSchema() }, ["text"]),
        Tool("diff_json", "Compare two JSON5-compatible documents.", new { left = StructuredTextSchema(), right = StructuredTextSchema(), onlyDifferences = BooleanSchema(true) }, ["left", "right"]),
        Tool("calculate_nd_exposure", "Calculate exposure time with a neutral density filter.", new { baseSeconds = NumberSchema(), stops = NumberSchema() }, ["baseSeconds", "stops"]),
        Tool("calculate_exposure_equivalence", "Calculate equivalent shutter speed after an aperture change.", new { shutterSeconds = NumberSchema(), originalAperture = NumberSchema(), targetAperture = NumberSchema() }, ["shutterSeconds", "originalAperture", "targetAperture"]),
        Tool("calculate_depth_of_field", "Calculate depth of field.", new { focalLengthMm = NumberSchema(), aperture = NumberSchema(), focusDistance = NumberSchema(), focusUnit = EnumSchema("m", "ft"), circleOfConfusionMm = NumberSchema() }, ["focalLengthMm", "aperture", "focusDistance", "focusUnit", "circleOfConfusionMm"]),
        Tool("generate_svg_placeholder", "Generate an SVG placeholder image.", new { width = IntegerSchema(1, 4096), height = IntegerSchema(1, 4096), fontSize = new { type = "integer", minimum = 1, maximum = 512, @default = 26 }, bgColor = new { type = "string", pattern = "^#[0-9a-f]{6}$", @default = "#cccccc" }, fgColor = new { type = "string", pattern = "^#[0-9a-f]{6}$", @default = "#333333" }, useExactSize = BooleanSchema(true), customText = new { type = "string", maxLength = 256, @default = "" } }, ["width", "height"]),
        Tool("calculate_chmod", "Calculate octal and symbolic Unix permissions.", new { permissions = PermissionSetSchema() }, ["permissions"]),
        Tool("expand_ipv4_range", "Find the smallest covering CIDR block for an IPv4 address range.", new { startIp = new { type = "string", maxLength = 15 }, endIp = new { type = "string", maxLength = 15 } }, ["startIp", "endIp"]),
        Tool("calculate_ipv4_subnet", "Calculate IPv4 network information.", new { address = new { type = "string", minLength = 1, maxLength = 32 } }, ["address"]),
        Tool("generate_ipv6_ula", "Generate IPv6 unique local address ranges from a MAC address.", new { macAddress = new { type = "string", pattern = "^([0-9a-f]{2}:){5}[0-9a-f]{2}$" }, timestampMs = new { type = "integer", minimum = 0L, maximum = 8_640_000_000_000_000L } }, ["macAddress"]),
        Tool("json_to_csv", "Convert an array of JSON objects to CSV.", new { rows = new { type = "array", minItems = 1, maxItems = 100, items = new { type = "object" } } }, ["rows"]),
        Tool("parse_url", "Parse a URL into its parts and query parameters.", new { url = StringSchema() }, ["url"]),
        Tool("generate_spf_record", "Build an SPF TXT record.", new { providers = StringArraySchema(15), ipAddresses = StringArraySchema(20), enforcement = EnumSchema("-all", "~all", "?all") }, []),
        Tool("generate_dmarc_record", "Build a DMARC TXT record.", new { policy = EnumSchema("reject", "quarantine", "none"), subdomainPolicy = EnumSchema("reject", "quarantine", "none", ""), percentage = new { type = "integer", minimum = 0, maximum = 100, @default = 100 }, ruaEmails = StringArraySchema(5), rufEmails = StringArraySchema(5), adkim = EnumSchema("", "s"), aspf = EnumSchema("", "s") }, []),
        Tool("parse_email_headers", "Parse message headers, delivery hops, authentication results, and spam signals.", new { headers = new { type = "string", minLength = 1, maxLength = 16_384 } }, ["headers"]),
        Tool("test_regex", "Match text using safe RE2 syntax.", new { pattern = new { type = "string", minLength = 1, maxLength = 256 }, text = new { type = "string", maxLength = 4096 }, global = BooleanSchema(true), ignoreCase = BooleanSchema(false), multiline = BooleanSchema(false), dotAll = BooleanSchema(true) }, ["pattern", "text"]),
        Tool("encode_file_base64_local", "Read a local file and return a bounded Base64 representation.", new { path = new { type = "string", minLength = 1, maxLength = 1024 } }, ["path"]),
        Tool("decode_file_base64_local", "Decode Base64 to a new local file. Existing files are never overwritten.", new { path = new { type = "string", minLength = 1, maxLength = 1024 }, base64 = new { type = "string", minLength = 1, maxLength = 43_692 } }, ["path", "base64"]),
        Tool("otp_private", "Generate or verify a six-digit HOTP or TOTP code locally.", new { secret = new { type = "string", minLength = 8, maxLength = 128 }, mode = EnumSchema("totp", "hotp"), counter = new { type = "integer", minimum = 0L, maximum = 9_007_199_254_740_991L }, timeStep = new { type = "integer", minimum = 15, maximum = 300, @default = 30 }, code = new { type = "string", pattern = "^\\d{6}$" } }, ["secret"]),
        Tool("generate_otp_secret_private", "Generate a random Base32 OTP secret and an otpauth URI locally.", new { issuer = new { type = "string", minLength = 1, maxLength = 128, @default = "killer-tools" }, account = new { type = "string", minLength = 1, maxLength = 128, @default = "demo-user" } }, []),
        Tool("generate_ulids", "Generate up to 100 ULIDs.", new { count = new { type = "integer", minimum = 1, maximum = 100, @default = 1 } }, []),
        Tool("generate_uuids", "Generate up to 100 UUIDs.", new { version = EnumSchema("NIL", "v1", "v3", "v4", "v5"), count = new { type = "integer", minimum = 1, maximum = 100, @default = 1 }, name = new { type = "string", maxLength = 128 }, @namespace = new { type = "string", maxLength = 36 } }, []),
        Tool("parse_jwt_private", "Decode a JWT locally without claiming to verify its signature.", new { token = new { type = "string", minLength = 3, maxLength = 16_384 } }, ["token"]),
        Tool("generate_rsa_keypair_private", "Generate an RSA PEM key pair locally.", new { bits = EnumSchema("2048", "3072", "4096") }, []),
        Tool("analyze_password_private", "Estimate password strength and crack time locally.", new { password = new { type = "string", maxLength = 8192 } }, ["password"]),
        Tool("evaluate_math", "Evaluate a bounded arithmetic expression.", new { expression = new { type = "string", minLength = 1, maxLength = 256 } }, ["expression"]),
        Tool("list_film_stocks", "List the film stocks available in KillerTools Reciprocity Calculator.", new { }, []),
        Tool("calculate_reciprocity", "Calculate reciprocity failure adjustment for a film stock.", new { filmStockId = new { type = "string", minLength = 1, maxLength = 64 }, meteredSeconds = new { type = "number", minimum = 0.0001, maximum = 3600 } }, ["filmStockId", "meteredSeconds"]),
        Tool("killermcp_update_status", "Check the installed KillerMCP version and whether a newer signed Windows installer is available.", new { }, []),
    ];

    public static object[] Tools => CoreTools.Concat(Adapters.Values.Select(adapter => new
    {
        name = adapter.Tool.Name,
        description = adapter.Tool.Description,
        inputSchema = adapter.Tool.InputSchema,
    })).ToArray<object>();

    public static void SetAdapters(IEnumerable<AppAdapter> adapters) => Adapters = adapters.ToDictionary(adapter => adapter.Tool.Name, StringComparer.Ordinal);

    public static object Call(JsonElement parameters, JsonSerializerOptions json)
    {
        var name = parameters.GetProperty("name").GetString();
        var arguments = parameters.GetProperty("arguments");
        try
        {
            if (name is not null && Adapters.TryGetValue(name, out var adapter))
            {
                var adapterResult = adapter.CallAsync(arguments, CancellationToken.None).GetAwaiter().GetResult();
                return adapterResult.IsError
                    ? new { content = new[] { new { type = "text", text = adapterResult.Text } }, isError = true }
                    : new { content = new[] { new { type = "text", text = adapterResult.Text } }, isError = false };
            }

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
            "lookup_exchange_ndr" => Localize("exchange-ndr-lookup", MicrosoftReferenceLookups.SearchNdrs(Query(arguments), Limit(arguments)), Locale(arguments)),
            "lookup_group_policy" => Localize("group-policy-reference", GroupPolicyLookup.Search(Query(arguments), Limit(arguments)), Locale(arguments)),
            "lookup_http_status" => Localize("http-status-codes", HttpStatusLookup.Search(Query(arguments), Limit(arguments)), Locale(arguments)),
            "lookup_m365_sku" => Localize("m365-sku-decoder", MicrosoftReferenceLookups.SearchSkus(Query(arguments), Limit(arguments)), Locale(arguments)),
            "lookup_port_protocol" => Localize("port-protocol-reference", PortProtocolLookup.Search(Query(arguments), Limit(arguments)), Locale(arguments)),
            "lookup_windows_error" => Localize("windows-error-codes", MicrosoftReferenceLookups.SearchErrors(Query(arguments), Limit(arguments)), Locale(arguments)),
            "lookup_windows_event" => Localize("windows-event-lookup", MicrosoftReferenceLookups.SearchEvents(Query(arguments), Limit(arguments)), Locale(arguments)),
            "list_killer_modules" => KillerCatalogs.ListModules(OptionalNullableString(arguments, "name")),
            "list_killer_scripts" => KillerScripts(arguments),
            "diff_text" => TextDiff.Compare(arguments.GetProperty("left").GetString()!, arguments.GetProperty("right").GetString()!),
            "format_json" => JsonFormat(arguments),
            "minify_json" => JsonMinify(arguments),
            "diff_json" => JsonDifference(arguments),
            "calculate_nd_exposure" => new { seconds = ExposureCalculators.WithNeutralDensity(arguments.GetProperty("baseSeconds").GetDouble(), arguments.GetProperty("stops").GetDouble()) },
            "calculate_exposure_equivalence" => new { shutterSeconds = ExposureCalculators.EquivalentShutter(arguments.GetProperty("shutterSeconds").GetDouble(), arguments.GetProperty("originalAperture").GetDouble(), arguments.GetProperty("targetAperture").GetDouble()) },
            "calculate_depth_of_field" => ExposureCalculators.DepthOfField(arguments.GetProperty("focalLengthMm").GetDouble(), arguments.GetProperty("aperture").GetDouble(), arguments.GetProperty("focusDistance").GetDouble(), arguments.GetProperty("focusUnit").GetString()!, arguments.GetProperty("circleOfConfusionMm").GetDouble()),
            "generate_svg_placeholder" => Svg(arguments),
            "calculate_chmod" => Chmod(arguments),
            "expand_ipv4_range" => Ipv4Tools.CoverRange(arguments.GetProperty("startIp").GetString()!, arguments.GetProperty("endIp").GetString()!),
            "calculate_ipv4_subnet" => Ipv4Subnet(arguments),
            "generate_ipv6_ula" => Ipv6Ula.Generate(arguments.GetProperty("macAddress").GetString()!, arguments.TryGetProperty("timestampMs", out var timestamp) ? timestamp.GetInt64() : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()),
            "json_to_csv" => new { csv = JsonCsv.Convert(arguments.GetProperty("rows")) },
            "parse_url" => UrlParser.Parse(arguments.GetProperty("url").GetString()!),
            "generate_spf_record" => new { record = EmailRecords.BuildSpf(StringArray(arguments, "providers"), StringArray(arguments, "ipAddresses"), OptionalString(arguments, "enforcement", "-all")) },
            "generate_dmarc_record" => new { record = EmailRecords.BuildDmarc(OptionalString(arguments, "policy", "reject"), OptionalString(arguments, "subdomainPolicy", ""), OptionalInteger(arguments, "percentage", 100), StringArray(arguments, "ruaEmails"), StringArray(arguments, "rufEmails"), OptionalString(arguments, "adkim", ""), OptionalString(arguments, "aspf", "")) },
            "parse_email_headers" => EmailHeaderParser.Parse(arguments.GetProperty("headers").GetString()!),
            "test_regex" => SafeRegex.Test(arguments.GetProperty("pattern").GetString()!, arguments.GetProperty("text").GetString()!, OptionalBoolean(arguments, "global", true), OptionalBoolean(arguments, "ignoreCase"), OptionalBoolean(arguments, "multiline"), OptionalBoolean(arguments, "dotAll", true)),
            "encode_file_base64_local" => EncodeFile(arguments),
            "decode_file_base64_local" => DecodeFile(arguments),
            "otp_private" => Otp(arguments),
            "generate_otp_secret_private" => OtpTools.GenerateSecret(OptionalString(arguments, "issuer", "killer-tools"), OptionalString(arguments, "account", "demo-user")),
            "generate_ulids" => new { ids = IdentifierGenerators.GenerateUlids(OptionalInteger(arguments, "count", 1)) },
            "generate_uuids" => Uuids(arguments),
            "parse_jwt_private" => Jwt(arguments),
            "generate_rsa_keypair_private" => RsaKeyPairs.Generate(int.Parse(OptionalString(arguments, "bits", "2048"), System.Globalization.CultureInfo.InvariantCulture)),
            "analyze_password_private" => PasswordStrength.Analyze(arguments.GetProperty("password").GetString()!),
            "evaluate_math" => EvaluateMath(arguments),
            "list_film_stocks" => FilmReciprocity.List(),
            "calculate_reciprocity" => Reciprocity(arguments),
            "killermcp_update_status" => UpdateStatus(arguments),
            _ => throw new InvalidOperationException($"Unknown tool: {name}"),
            };
            return new { content = new[] { new { type = "text", text = JsonSerializer.Serialize(value, json) } } };
        }
        catch (ToolCallException exception)
        {
            return new { content = new[] { new { type = "text", text = exception.Message } }, isError = true };
        }
    }

    private static object Tool(string name, string description, object properties, string[] required) =>
        new { name, description, inputSchema = new { type = "object", properties, required, additionalProperties = false } };
    private static object LookupTool(string name, string description) =>
        Tool(name, description, new { query = StringSchema(), limit = new { type = "integer", minimum = 1, maximum = 20, @default = 10 }, locale = EnumSchema("en", "bn", "cs", "de", "es", "fr", "hu", "it", "ja", "kk", "no", "pl", "pt", "ru", "tr", "uk", "vi", "zh", "zh-TW") }, ["query"]);
    private static object StringSchema() => new { type = "string", maxLength = 4096 };
    private static object StructuredTextSchema() => new { type = "string", minLength = 1, maxLength = 4096 };
    private static object BooleanSchema(bool defaultValue) => new { type = "boolean", @default = defaultValue };
    private static object IntegerSchema(int minimum, int maximum) => new { type = "integer", minimum, maximum };
    private static object NumberSchema() => new { type = "number" };
    private static object EnumSchema(params string[] values) => new { type = "string", @enum = values };
    private static object StringArraySchema(int maximum) => new { type = "array", maxItems = maximum, items = new { type = "string" }, @default = Array.Empty<string>() };
    private static object PermissionSetSchema()
    {
        var group = new { type = "object", properties = new { read = new { type = "boolean" }, write = new { type = "boolean" }, execute = new { type = "boolean" } }, required = new[] { "read", "write", "execute" }, additionalProperties = false };
        return new { type = "object", properties = new { owner = group, group, @public = group }, required = new[] { "owner", "group", "public" }, additionalProperties = false };
    }
    private static bool OptionalBoolean(JsonElement arguments, string name, bool defaultValue = false) => arguments.TryGetProperty(name, out var value) ? value.GetBoolean() : defaultValue;
    private static string OptionalString(JsonElement arguments, string name, string defaultValue) => arguments.TryGetProperty(name, out var value) ? value.GetString()! : defaultValue;
    private static int OptionalInteger(JsonElement arguments, string name, int defaultValue) => arguments.TryGetProperty(name, out var value) ? value.GetInt32() : defaultValue;
    private static string[] StringArray(JsonElement arguments, string name) => arguments.TryGetProperty(name, out var values) ? values.EnumerateArray().Select(value => value.GetString()!).ToArray() : [];
    private static string Query(JsonElement arguments) => arguments.GetProperty("query").GetString()!;
    private static int Limit(JsonElement arguments) => arguments.TryGetProperty("limit", out var value) ? value.GetInt32() : 10;
    private static object UpdateStatus(JsonElement arguments)
    {
        if (arguments.EnumerateObject().Any())
        {
            throw new ToolCallException("This tool takes no arguments");
        }

        return Status;
    }
    private static string Locale(JsonElement arguments) => OptionalString(arguments, "locale", "en");
    private static object Localize<T>(string tool, IReadOnlyList<T> entries, string locale)
    {
        if (locale.Equals("en", StringComparison.OrdinalIgnoreCase))
        {
            return entries;
        }

        var source = JsonSerializer.SerializeToElement(entries, CamelCaseJson);
        return TranslateElement(tool, locale, source)!;
    }

    private static object? TranslateElement(string tool, string locale, JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().ToDictionary(property => property.Name, property => TranslateElement(tool, locale, property.Value)),
        JsonValueKind.Array => element.EnumerateArray().Select(item => TranslateElement(tool, locale, item)).ToArray(),
        JsonValueKind.String => ReferenceTranslations.Translate(tool, locale, element.GetString()!),
        JsonValueKind.Number => element.TryGetInt64(out var integer) ? integer : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => element.GetRawText(),
    };
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
    private static object JsonFormat(JsonElement arguments)
    {
        try
        {
            var text = StructuredJson.Format(arguments.GetProperty("text").GetString()!, OptionalInteger(arguments, "indentSize", 3), OptionalBoolean(arguments, "sortKeys", true));
            if (text.Length > 16_384) throw new ToolCallException("Output is too large");
            return new { text };
        }
        catch (ToolCallException) { throw; }
        catch (Exception exception) when (exception is FormatException or ArgumentException)
        {
            throw new ToolCallException("Invalid structured text");
        }
    }
    private static object JsonMinify(JsonElement arguments)
    {
        try
        {
            var text = StructuredJson.Minify(arguments.GetProperty("text").GetString()!);
            if (text.Length > 16_384) throw new ToolCallException("Output is too large");
            return new { text };
        }
        catch (ToolCallException) { throw; }
        catch (Exception exception) when (exception is FormatException or ArgumentException)
        {
            throw new ToolCallException("Invalid structured text");
        }
    }
    private static object JsonDifference(JsonElement arguments)
    {
        try
        {
            var value = StructuredJson.Diff(arguments.GetProperty("left").GetString()!, arguments.GetProperty("right").GetString()!, OptionalBoolean(arguments, "onlyDifferences", true));
            if (JsonSerializer.Serialize(value, CamelCaseJson).Length > 16_384) throw new ToolCallException("Output is too large");
            return value;
        }
        catch (ToolCallException) { throw; }
        catch (Exception exception) when (exception is FormatException or ArgumentException)
        {
            throw new ToolCallException("Invalid document input");
        }
    }
    private static object KillerScripts(JsonElement arguments)
    {
        try
        {
            return KillerCatalogs.ListScriptsAsync(OptionalString(arguments, "query", "")).GetAwaiter().GetResult();
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidDataException or JsonException)
        {
            throw new ToolCallException("Script catalog is unavailable");
        }
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
    private static object Ipv4Subnet(JsonElement arguments)
    {
        var subnet = Ipv4Tools.CalculateSubnet(arguments.GetProperty("address").GetString()!);
        var value = new Dictionary<string, object?>
        {
            ["cidr"] = subnet.Cidr,
            ["networkAddress"] = subnet.NetworkAddress,
            ["networkMask"] = subnet.NetworkMask,
            ["prefixLength"] = subnet.PrefixLength,
            ["wildcardMask"] = subnet.WildcardMask,
            ["size"] = subnet.Size,
            ["usableHosts"] = subnet.UsableHosts,
            ["firstAddress"] = subnet.FirstAddress,
            ["lastAddress"] = subnet.LastAddress,
            ["ipClass"] = subnet.IpClass,
        };
        if (subnet.BroadcastAddress is not null) value["broadcastAddress"] = subnet.BroadcastAddress;
        return value;
    }
    private static object EncodeFile(JsonElement arguments)
    {
        try
        {
            return Base64Files.Encode(arguments.GetProperty("path").GetString()!);
        }
        catch (InvalidDataException exception)
        {
            throw new ToolCallException(exception.Message);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            throw new ToolCallException("Unable to read file");
        }
    }
    private static object DecodeFile(JsonElement arguments)
    {
        try
        {
            return Base64Files.Decode(arguments.GetProperty("path").GetString()!, arguments.GetProperty("base64").GetString()!);
        }
        catch (FormatException exception)
        {
            throw new ToolCallException(exception.Message);
        }
        catch (InvalidDataException exception)
        {
            throw new ToolCallException(exception.Message);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            throw new ToolCallException("Unable to write file. Choose a new path.");
        }
    }
    private static object Otp(JsonElement arguments)
    {
        try
        {
            var mode = OptionalString(arguments, "mode", "totp");
            var timeStep = OptionalInteger(arguments, "timeStep", 30);
            var counter = mode == "totp"
                ? DateTimeOffset.UtcNow.ToUnixTimeSeconds() / timeStep
                : arguments.TryGetProperty("counter", out var value) ? value.GetInt64() : throw new ToolCallException("A counter is required for HOTP");
            var secret = arguments.GetProperty("secret").GetString()!;
            if (arguments.TryGetProperty("code", out var code))
            {
                return new { matches = OtpTools.VerifyCode(secret, counter, code.GetString()!) };
            }
            return new { code = OtpTools.GenerateCode(secret, counter), counter };
        }
        catch (ToolCallException)
        {
            throw;
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException)
        {
            throw new ToolCallException("Invalid OTP secret");
        }
    }
    private static object Uuids(JsonElement arguments)
    {
        try
        {
            var ids = IdentifierGenerators.GenerateUuids(OptionalString(arguments, "version", "v4"), OptionalInteger(arguments, "count", 1), OptionalNullableString(arguments, "name"), OptionalNullableString(arguments, "namespace"));
            return new { ids };
        }
        catch (FormatException exception)
        {
            throw new ToolCallException(exception.Message);
        }
    }
    private static object Jwt(JsonElement arguments)
    {
        try
        {
            return JwtParser.Decode(arguments.GetProperty("token").GetString()!);
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            throw new ToolCallException("Invalid JWT");
        }
    }
    private static object EvaluateMath(JsonElement arguments)
    {
        try
        {
            return new { value = BoundedMath.Evaluate(arguments.GetProperty("expression").GetString()!) };
        }
        catch (Exception exception) when (exception is FormatException or ArithmeticException)
        {
            throw new ToolCallException("Invalid or unsupported math expression");
        }
    }
    private static object Reciprocity(JsonElement arguments)
    {
        try
        {
            return FilmReciprocity.Calculate(arguments.GetProperty("filmStockId").GetString()!, arguments.GetProperty("meteredSeconds").GetDouble());
        }
        catch (KeyNotFoundException exception)
        {
            throw new ToolCallException(exception.Message);
        }
    }

    private static string? OptionalNullableString(JsonElement arguments, string name) => arguments.TryGetProperty(name, out var value) ? value.GetString() : null;
}

sealed class ToolCallException(string message) : Exception(message);
