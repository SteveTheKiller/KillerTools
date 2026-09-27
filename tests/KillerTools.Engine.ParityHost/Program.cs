using System.Text;
using System.Text.Json;
using KillerTools.Engine.Math;
using KillerTools.Engine.Numbers;
using KillerTools.Engine.Text;
using KillerTools.Engine.Documents;
using KillerTools.Engine.SystemTools;
using KillerTools.Engine.Photography;
using KillerTools.Engine.Network;
using KillerTools.Engine.Reference;
using KillerTools.Engine.Security;

Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = Encoding.UTF8;

using var input = JsonDocument.Parse(Console.In.ReadToEnd());
var output = new List<object>();

foreach (var request in input.RootElement.EnumerateArray())
{
    var operation = request.GetProperty("operation").GetString();
    var arguments = request.GetProperty("arguments");
    output.Add(operation switch
    {
        "text_statistics" => TextStatisticsResult(arguments),
        "convert_case" => CaseConverter.Convert(arguments.GetProperty("text").GetString()!).Select(item => new { label = item.Label, value = item.Value }),
        "convert_date_time" => DateTimeResult(arguments),
        "calculate_percentage" => PercentageResult(arguments),
        "encode_base64" => new { encoded = Base64Text.Encode(arguments.GetProperty("text").GetString()!, arguments.TryGetProperty("urlSafe", out var encodeSafe) && encodeSafe.GetBoolean()) },
        "decode_base64" => new { text = Base64Text.Decode(arguments.GetProperty("encoded").GetString()!, arguments.TryGetProperty("urlSafe", out var decodeSafe) && decodeSafe.GetBoolean()) },
        "text_to_ascii_binary" => new { binary = AsciiBinary.Encode(arguments.GetProperty("text").GetString()!) },
        "ascii_binary_to_text" => new { text = AsciiBinary.Decode(arguments.GetProperty("binary").GetString()!) },
        "arabic_to_roman" => new { roman = RomanNumerals.FromArabic(arguments.GetProperty("number").GetInt32()) },
        "roman_to_arabic" => new { number = RomanNumerals.ToArabic(arguments.GetProperty("roman").GetString()!) },
        "text_to_nato_alphabet" => new { nato = NatoAlphabet.Convert(arguments.GetProperty("text").GetString()!) },
        "convert_integer_base" => new { value = IntegerBases.Convert(arguments.GetProperty("value").GetString()!, arguments.GetProperty("fromBase").GetInt32(), arguments.GetProperty("toBase").GetInt32()) },
        "convert_temperature" => Temperatures.Convert(arguments.GetProperty("value").GetDouble(), arguments.GetProperty("scale").GetString()!),
        "calculate_chmod" => ChmodResult(arguments),
        "generate_svg_placeholder" => SvgResult(arguments),
        "calculate_nd_exposure" => new { seconds = ExposureCalculators.WithNeutralDensity(arguments.GetProperty("baseSeconds").GetDouble(), arguments.GetProperty("stops").GetDouble()) },
        "calculate_exposure_equivalence" => new { shutterSeconds = ExposureCalculators.EquivalentShutter(arguments.GetProperty("shutterSeconds").GetDouble(), arguments.GetProperty("originalAperture").GetDouble(), arguments.GetProperty("targetAperture").GetDouble()) },
        "calculate_depth_of_field" => DepthOfFieldResult(arguments),
        "escape_html_entities" => new { text = HtmlEntities.Escape(arguments.GetProperty("text").GetString()!) },
        "unescape_html_entities" => new { text = HtmlEntities.Unescape(arguments.GetProperty("text").GetString()!) },
        "diff_text" => TextDiffResult(arguments),
        "expand_ipv4_range" => Ipv4RangeResult(arguments),
        "calculate_ipv4_subnet" => Ipv4SubnetResult(arguments),
        "generate_ipv6_ula" => Ipv6UlaResult(arguments),
        "format_json" => new { text = StructuredJson.Format(arguments.GetProperty("text").GetString()!, arguments.GetProperty("indentSize").GetInt32(), arguments.GetProperty("sortKeys").GetBoolean()) },
        "minify_json" => new { text = StructuredJson.Minify(arguments.GetProperty("text").GetString()!) },
        "diff_json" => StructuredJson.Diff(arguments.GetProperty("left").GetString()!, arguments.GetProperty("right").GetString()!, arguments.GetProperty("onlyDifferences").GetBoolean()),
        "convert_xml_json" => new { text = arguments.GetProperty("direction").GetString() == "xml_to_json" ? XmlJsonConverter.XmlToJson(arguments.GetProperty("text").GetString()!) : XmlJsonConverter.JsonToXml(arguments.GetProperty("text").GetString()!) },
        "format_xml" => new { text = XmlFormatter.Format(arguments.GetProperty("text").GetString()!, arguments.GetProperty("indentSize").GetInt32(), arguments.GetProperty("collapseContent").GetBoolean()) },
        "convert_json" => StructuredConversion(arguments, "json"),
        "convert_yaml" => StructuredConversion(arguments, "yaml"),
        "convert_toml" => StructuredConversion(arguments, "toml"),
        "format_yaml" => new { text = StructuredTextConverter.FormatYaml(arguments.GetProperty("text").GetString()!, arguments.GetProperty("indentSize").GetInt32(), arguments.GetProperty("sortKeys").GetBoolean()) },
        "json_to_csv" => new { csv = JsonCsv.Convert(arguments.GetProperty("rows")) },
        "parse_url" => UrlResult(arguments),
        "generate_spf_record" => SpfResult(arguments),
        "generate_dmarc_record" => DmarcResult(arguments),
        "parse_email_headers" => EmailHeadersResult(arguments),
        "test_regex" => RegexResult(arguments),
        "lookup_http_status" => HttpStatusResult(arguments),
        "lookup_port_protocol" => PortProtocolResult(arguments),
        "lookup_group_policy" => GroupPolicyLookup.Search(arguments.GetProperty("query").GetString()!, arguments.GetProperty("limit").GetInt32())
            .Select(item => new { category = item.Category, name = item.Name, path = item.Path, registry = item.Registry, defaultValue = item.DefaultValue, recommendedValue = item.RecommendedValue, description = item.Description, severity = item.Severity }),
        "lookup_exchange_ndr" => MicrosoftReferenceResult(operation, arguments),
        "lookup_m365_sku" => MicrosoftReferenceResult(operation, arguments),
        "lookup_windows_error" => MicrosoftReferenceResult(operation, arguments),
        "lookup_windows_event" => MicrosoftReferenceResult(operation, arguments),
        "list_killer_modules" => KillerCatalogs.ListModules(arguments.TryGetProperty("name", out var moduleName) ? moduleName.GetString() : null),
        "list_killer_scripts" => (await KillerCatalogs.ListScriptsAsync(arguments.TryGetProperty("query", out var scriptQuery) ? scriptQuery.GetString() : null)).Select(script => new { filename = script.Filename, name = script.Name, description = script.Description, downloadUrl = script.DownloadUrl }),
        "encode_file_base64_local" => EncodedFileResult(arguments),
        "otp_private" => OtpResult(arguments),
        "generate_uuids" => new { ids = IdentifierGenerators.GenerateUuids(arguments.GetProperty("version").GetString()!, arguments.GetProperty("count").GetInt32(), arguments.TryGetProperty("name", out var name) ? name.GetString() : null, arguments.TryGetProperty("namespace", out var namespaceValue) ? namespaceValue.GetString() : null) },
        "generate_lorem_ipsum" => new { text = LoremIpsum.Generate(arguments.GetProperty("paragraphCount").GetInt32(), arguments.GetProperty("sentencePerParagraph").GetInt32(), arguments.GetProperty("wordCount").GetInt32(), arguments.GetProperty("startWithLoremIpsum").GetBoolean(), arguments.GetProperty("asHTML").GetBoolean()) },
        "parse_jwt_private" => JwtResult(arguments),
        "analyze_password_private" => PasswordStrengthResult(arguments),
        "evaluate_math" => new { value = BoundedMath.Evaluate(arguments.GetProperty("expression").GetString()!) },
        "list_film_stocks" => FilmReciprocity.List().Select(stock => new { id = stock.Id, name = stock.Name, manufacturer = stock.Manufacturer, iso = stock.Iso, type = stock.Type }),
        "calculate_reciprocity" => ReciprocityResult(arguments),
        "list_film_development_options" => DevelopmentOptionsResult(),
        "calculate_film_development" => DevelopmentResult(arguments),
        _ => throw new InvalidOperationException($"Unknown parity operation: {operation}"),
    });
}

static object DateTimeResult(JsonElement arguments)
{
    var value = DateTimeConverter.Convert(arguments.GetProperty("value").GetString()!, arguments.GetProperty("inputFormat").GetString()!);
    return new { iso8601 = value.Iso8601, iso9075 = value.Iso9075, rfc3339 = value.Rfc3339, rfc7231 = value.Rfc7231, unixSeconds = value.UnixSeconds, timestampMs = value.TimestampMs, utc = value.Utc, mongoObjectId = value.MongoObjectId, excel = value.Excel };
}

static object StructuredConversion(JsonElement arguments, string from)
{
    var text = arguments.GetProperty("text").GetString()!;
    var to = arguments.GetProperty("to").GetString();
    var converted = (from, to) switch
    {
        ("json", "yaml") => StructuredTextConverter.JsonToYaml(text),
        ("json", "toml") => StructuredTextConverter.JsonToToml(text),
        ("yaml", "json") => StructuredTextConverter.YamlToJson(text),
        ("yaml", "toml") => StructuredTextConverter.YamlToToml(text),
        ("toml", "json") => StructuredTextConverter.TomlToJson(text),
        ("toml", "yaml") => StructuredTextConverter.TomlToYaml(text),
        _ => throw new InvalidOperationException(),
    };
    return new { text = converted };
}

static object DevelopmentOptionsResult() => new
{
    developers = FilmDevelopment.Developers.Select(developer => new
    {
        id = developer.Id,
        name = developer.Name,
        dilutions = developer.Dilutions.Select(dilution => new { label = dilution.Label, devParts = dilution.DevParts, waterParts = dilution.WaterParts }),
        defaultDilution = developer.DefaultDilution,
    }),
    films = FilmDevelopment.Films.Select(film =>
    {
        var item = new Dictionary<string, object> { ["name"] = film.Name, ["isoBase"] = film.IsoBase };
        if (film.Process is not null) item["process"] = film.Process;
        return item;
    }),
};

static object DevelopmentResult(JsonElement arguments)
{
    var value = FilmDevelopment.Calculate(
        arguments.GetProperty("filmName").GetString()!,
        arguments.GetProperty("developerId").GetString()!,
        arguments.TryGetProperty("dilutionIndex", out var dilutionIndex) ? dilutionIndex.GetInt32() : null,
        arguments.TryGetProperty("baseSeconds", out var baseSeconds) ? baseSeconds.GetDouble() : null,
        arguments.TryGetProperty("temperatureC", out var temperature) ? temperature.GetDouble() : null,
        arguments.TryGetProperty("pushPullStops", out var stops) ? stops.GetDouble() : 0,
        arguments.TryGetProperty("tankMl", out var tank) ? tank.GetDouble() : 500);
    return new { baseSeconds = value.BaseSeconds, temperatureAdjustedSeconds = value.TemperatureAdjustedSeconds, finalSeconds = value.FinalSeconds, displayTime = value.DisplayTime, dilution = value.Dilution, devMl = value.DevMl, waterMl = value.WaterMl, process = value.Process };
}

static object EncodedFileResult(JsonElement arguments)
{
    var value = Base64Files.Encode(arguments.GetProperty("path").GetString()!);
    return new { base64 = value.Base64, byteLength = value.ByteLength };
}

static object OtpResult(JsonElement arguments)
{
    var secret = arguments.GetProperty("secret").GetString()!;
    var counter = arguments.GetProperty("counter").GetInt64();
    if (arguments.TryGetProperty("code", out var code))
    {
        return new { matches = OtpTools.VerifyCode(secret, counter, code.GetString()!) };
    }
    return new { code = OtpTools.GenerateCode(secret, counter), counter };
}

static object JwtResult(JsonElement arguments)
{
    var value = JwtParser.Decode(arguments.GetProperty("token").GetString()!);
    return new { header = value.Header, payload = value.Payload, headerClaims = value.HeaderClaims, payloadClaims = value.PayloadClaims, signatureVerified = value.SignatureVerified };
}

static object PasswordStrengthResult(JsonElement arguments)
{
    var value = PasswordStrength.Analyze(arguments.GetProperty("password").GetString()!);
    return new { entropy = value.Entropy, charsetLength = value.CharsetLength, passwordLength = value.PasswordLength, crackDurationFormatted = value.CrackDurationFormatted, secondsToCrack = value.SecondsToCrack, score = value.Score };
}

static object ReciprocityResult(JsonElement arguments)
{
    var value = FilmReciprocity.Calculate(arguments.GetProperty("filmStockId").GetString()!, arguments.GetProperty("meteredSeconds").GetDouble());
    return new { adjustedSeconds = value.AdjustedSeconds, extraStops = value.ExtraStops, noFailure = value.NoFailure };
}

static object MicrosoftReferenceResult(string operation, JsonElement arguments)
{
    var query = arguments.GetProperty("query").GetString()!;
    var limit = arguments.GetProperty("limit").GetInt32();
    return operation switch
    {
        "lookup_exchange_ndr" => MicrosoftReferenceLookups.SearchNdrs(query, limit).Select(item => new { category = item.Category, code = item.Code, name = item.Name, description = item.Description, cause = item.Cause, fix = item.Fix, severity = item.Severity }),
        "lookup_m365_sku" => MicrosoftReferenceLookups.SearchSkus(query, limit).Select(item => new { category = item.Category, guid = item.Guid, stringId = item.StringId, name = item.Name, description = item.Description, tier = item.Tier }),
        "lookup_windows_error" => MicrosoftReferenceLookups.SearchErrors(query, limit).Select(item => new { category = item.Category, hex = item.Hex, decimalValue = item.Decimal, name = item.Name, description = item.Description, severity = item.Severity }).Select(item => new Dictionary<string, object> { ["category"] = item.category, ["hex"] = item.hex, ["decimal"] = item.decimalValue, ["name"] = item.name, ["description"] = item.description, ["severity"] = item.severity }),
        "lookup_windows_event" => MicrosoftReferenceLookups.SearchEvents(query, limit).Select(item => new { category = item.Category, log = item.Log, id = item.Id, name = item.Name, description = item.Description, severity = item.Severity }),
        _ => throw new InvalidOperationException(),
    };
}

static object PortProtocolResult(JsonElement arguments) => PortProtocolLookup.Search(arguments.GetProperty("query").GetString()!, arguments.GetProperty("limit").GetInt32())
    .Select(entry =>
    {
        var result = new Dictionary<string, object>
        {
            ["category"] = entry.Category, ["port"] = entry.Port, ["protocol"] = entry.Protocol,
            ["service"] = entry.Service, ["description"] = entry.Description, ["dangerous"] = entry.Dangerous,
        };
        if (entry.Notes is not null) result["notes"] = entry.Notes;
        return result;
    });

static object HttpStatusResult(JsonElement arguments) => HttpStatusLookup.Search(arguments.GetProperty("query").GetString()!, arguments.GetProperty("limit").GetInt32())
    .Select(entry => new { category = entry.Category, code = entry.Code, name = entry.Name, description = entry.Description, type = entry.Type });

static object RegexResult(JsonElement arguments)
{
    var value = SafeRegex.Test(arguments.GetProperty("pattern").GetString()!, arguments.GetProperty("text").GetString()!, arguments.GetProperty("global").GetBoolean(), arguments.GetProperty("ignoreCase").GetBoolean(), arguments.GetProperty("multiline").GetBoolean(), arguments.GetProperty("dotAll").GetBoolean());
    return new { matches = value.Matches.Select(match => new { index = match.Index, match = match.Match, groups = match.Groups }), truncated = value.Truncated, syntax = value.Syntax };
}

static object EmailHeadersResult(JsonElement arguments)
{
    var value = EmailHeaderParser.Parse(arguments.GetProperty("headers").GetString()!);
    return new
    {
        fields = value.Fields.Select(field => new { label = field.Label, value = field.Value }),
        hops = value.Hops.Select(hop => new { from = hop.From, by = hop.By, ip = hop.Ip, timestamp = hop.Timestamp, delay = hop.Delay }),
        auth = value.Auth.Select(auth => new { protocol = auth.Protocol, result = auth.Result, detail = auth.Detail }),
        spamScore = value.SpamScore,
        spamStatus = value.SpamStatus,
        scl = value.Scl,
        sclLabel = value.SclLabel,
        senderMismatch = value.SenderMismatch,
        sendingService = value.SendingService,
    };
}

static object SpfResult(JsonElement arguments) => new
{
    record = EmailRecords.BuildSpf(
        arguments.GetProperty("providers").EnumerateArray().Select(value => value.GetString()!),
        arguments.GetProperty("ipAddresses").EnumerateArray().Select(value => value.GetString()!),
        arguments.GetProperty("enforcement").GetString()!)
};

static object DmarcResult(JsonElement arguments) => new
{
    record = EmailRecords.BuildDmarc(
        arguments.GetProperty("policy").GetString()!, arguments.GetProperty("subdomainPolicy").GetString()!, arguments.GetProperty("percentage").GetInt32(),
        arguments.GetProperty("ruaEmails").EnumerateArray().Select(value => value.GetString()!),
        arguments.GetProperty("rufEmails").EnumerateArray().Select(value => value.GetString()!),
        arguments.GetProperty("adkim").GetString()!, arguments.GetProperty("aspf").GetString()!)
};

static object UrlResult(JsonElement arguments)
{
    var value = UrlParser.Parse(arguments.GetProperty("url").GetString()!);
    return new { protocol = value.Protocol, hostname = value.Hostname, port = value.Port, username = value.Username, password = value.Password, pathname = value.Pathname, search = value.Search, hash = value.Hash, searchParams = value.SearchParams };
}

static object Ipv4RangeResult(JsonElement arguments)
{
    var value = Ipv4Tools.CoverRange(arguments.GetProperty("startIp").GetString()!, arguments.GetProperty("endIp").GetString()!);
    return new { newEnd = value.NewEnd, newStart = value.NewStart, newCidr = value.NewCidr, newSize = value.NewSize, oldSize = value.OldSize };
}

static object Ipv4SubnetResult(JsonElement arguments)
{
    var value = Ipv4Tools.CalculateSubnet(arguments.GetProperty("address").GetString()!);
    var result = new Dictionary<string, object>
    {
        ["cidr"] = value.Cidr,
        ["networkAddress"] = value.NetworkAddress,
        ["networkMask"] = value.NetworkMask,
        ["prefixLength"] = value.PrefixLength,
        ["wildcardMask"] = value.WildcardMask,
        ["size"] = value.Size,
        ["usableHosts"] = value.UsableHosts,
        ["firstAddress"] = value.FirstAddress,
        ["lastAddress"] = value.LastAddress,
        ["ipClass"] = value.IpClass,
    };
    if (value.BroadcastAddress is not null)
    {
        result["broadcastAddress"] = value.BroadcastAddress;
    }
    return result;
}

static object Ipv6UlaResult(JsonElement arguments)
{
    var value = Ipv6Ula.Generate(arguments.GetProperty("macAddress").GetString()!, arguments.GetProperty("timestampMs").GetInt64());
    return new { ula48 = value.Ula48, firstRoutable64 = value.FirstRoutable64, lastRoutable64 = value.LastRoutable64, timestampMs = value.TimestampMs };
}

static object DepthOfFieldResult(JsonElement arguments)
{
    var value = ExposureCalculators.DepthOfField(arguments.GetProperty("focalLengthMm").GetDouble(), arguments.GetProperty("aperture").GetDouble(), arguments.GetProperty("focusDistance").GetDouble(), arguments.GetProperty("focusUnit").GetString()!, arguments.GetProperty("circleOfConfusionMm").GetDouble());
    return new { hyperfocalMeters = value.HyperfocalMeters, nearMeters = value.NearMeters, farMeters = value.FarMeters, depthOfFieldMeters = value.DepthOfFieldMeters };
}

static object TextDiffResult(JsonElement arguments)
{
    var value = TextDiff.Compare(arguments.GetProperty("left").GetString()!, arguments.GetProperty("right").GetString()!);
    return new
    {
        equal = value.Equal,
        added = value.Added,
        removed = value.Removed,
        lines = value.Lines.Select(line => new { kind = line.Kind, text = line.Text }),
    };
}

static object ChmodResult(JsonElement arguments)
{
    var permissions = arguments.GetProperty("permissions");
    PermissionGroup Group(string name)
    {
        var group = permissions.GetProperty(name);
        return new PermissionGroup(group.GetProperty("read").GetBoolean(), group.GetProperty("write").GetBoolean(), group.GetProperty("execute").GetBoolean());
    }
    var value = UnixPermissions.Calculate(new UnixPermissionSet(Group("owner"), Group("group"), Group("public")));
    return new { octal = value.Octal, symbolic = value.Symbolic, command = value.Command };
}

static object SvgResult(JsonElement arguments)
{
    var svg = SvgPlaceholder.Build(arguments.GetProperty("width").GetInt32(), arguments.GetProperty("height").GetInt32(), arguments.GetProperty("fontSize").GetInt32(), arguments.GetProperty("bgColor").GetString()!, arguments.GetProperty("fgColor").GetString()!, arguments.GetProperty("useExactSize").GetBoolean(), arguments.GetProperty("customText").GetString()!);
    return new { svg, dataUrl = $"data:image/svg+xml;base64,{Base64Text.Encode(svg)}" };
}

Console.Write(JsonSerializer.Serialize(output));

static object TextStatisticsResult(JsonElement arguments)
{
    var value = TextStatistics.Calculate(arguments.GetProperty("text").GetString()!);
    return new
    {
        charCount = value.CharacterCount,
        wordCount = value.WordCount,
        lineCount = value.LineCount,
        byteSize = value.ByteSize,
    };
}

static object PercentageResult(JsonElement arguments)
{
    var mode = arguments.GetProperty("mode").GetString();
    var x = arguments.GetProperty("x").GetDouble();
    var y = arguments.GetProperty("y").GetDouble();
    var value = mode switch
    {
        "percent_of" => Percentages.Of(x, y),
        "what_percent" => Percentages.Ratio(x, y),
        "change" => Percentages.Change(x, y),
        _ => throw new InvalidOperationException($"Unknown percentage mode: {mode}"),
    };
    return new { value };
}
