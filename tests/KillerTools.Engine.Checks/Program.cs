using KillerTools.Engine.Math;
using KillerTools.Engine.Numbers;
using KillerTools.Engine.Text;
using KillerTools.Engine.Documents;
using KillerTools.Engine.SystemTools;
using KillerTools.Engine.Photography;
using KillerTools.Engine.Network;
using KillerTools.Engine.Reference;
using KillerTools.Engine.Security;
using KillerTools.Engine.Web;

var checks = new (string Name, Action Run)[]
{
    ("text statistics", CheckTextStatistics),
    ("Unicode entities", CheckUnicodeEntities),
    ("percentages", CheckPercentages),
    ("text conversions", CheckTextConversions),
    ("FIGlet text", CheckAsciiArt),
    ("emoji search", CheckEmojiSearch),
    ("case conversions", CheckCaseConversions),
    ("Lorem Ipsum generation", CheckLoremIpsum),
    ("date time conversions", CheckDateTimeConversions),
    ("HTML entities and text diff", CheckHtmlAndDiff),
    ("Markdown conversion", CheckMarkdown),
    ("IP network calculations", CheckNetworkCalculations),
    ("DNS lookup", CheckDnsLookup),
    ("RDAP lookup", CheckRdapLookup),
    ("CVE lookup", CheckCveLookup),
    ("URL and CSV conversions", CheckUrlAndCsv),
    ("user agent parsing", CheckUserAgentParsing),
    ("phone number parsing", CheckPhoneNumberParsing),
    ("color conversion", CheckColorConversion),
    ("MAC vendor lookup", CheckMacVendorLookup),
    ("QR code generation", CheckQrCodes),
    ("JSON5 formatting and diff", CheckStructuredJson),
    ("XML and JSON conversion", CheckXmlJson),
    ("YAML and TOML conversion", CheckYamlToml),
    ("meta tag generation", CheckMetaTags),
    ("GIF search", CheckGifSearch),
    ("cron descriptions", CheckCronDescriptions),
    ("email records", CheckEmailRecords),
    ("email headers", CheckEmailHeaders),
    ("safe regular expressions", CheckSafeRegex),
    ("HTTP status lookup", CheckHttpStatusLookup),
    ("port and protocol lookup", CheckPortProtocolLookup),
    ("Microsoft reference lookups", CheckMicrosoftReferenceLookups),
    ("group policy lookup", CheckGroupPolicyLookup),
    ("reference translations", CheckReferenceTranslations),
    ("Killer module and script catalogs", CheckKillerCatalogs),
    ("PowerShell Builder catalog", CheckPowerShellBuilder),
    ("portable utilities", CheckPortableUtilities),
    ("bounded Base64 files", CheckBase64Files),
    ("one-time passwords", CheckOtp),
    ("identifier generators", CheckIdentifierGenerators),
    ("JWT decoding", CheckJwt),
    ("RSA key generation", CheckRsa),
    ("private text hashes", CheckTextHashes),
    ("CryptoJS text encryption", CheckCryptoJsText),
    ("bcrypt", CheckBcrypt),
    ("password strength", CheckPasswordStrength),
    ("password generation", CheckPasswordGeneration),
    ("BIP39", CheckBip39),
    ("bounded math", CheckBoundedMath),
    ("film reciprocity", CheckFilmReciprocity),
    ("film development", CheckFilmDevelopment),
};

foreach (var check in checks)
{
    check.Run();
    Console.WriteLine($"PASS {check.Name}");
}

static void CheckLoremIpsum()
{
    Equal("Lorem ipsum dolor sit amet, consectetur adipiscing elit.", LoremIpsum.Generate(1, 1, 4));
    Equal("<p>Lorem ipsum dolor sit amet, consectetur adipiscing elit.</p>", LoremIpsum.Generate(1, 1, 4, true, true));
    Throws<ArgumentOutOfRangeException>(() => LoremIpsum.Generate(11));
}

static void CheckAsciiArt()
{
    Equal("  _   _ _ \n | | | (_)\n | |_| | |\n |  _  | |\n |_| |_|_|\n          ", AsciiArt.Draw("Hi"));
    Equal("Ⓚⓘⓛⓛⓔⓡ", AsciiArt.Draw("Killer", "Circle", 20));
    Equal(317, AsciiArt.FontNames().Count);
    Throws<ArgumentException>(() => AsciiArt.Draw("test", "../font"));
    Throws<ArgumentOutOfRangeException>(() => AsciiArt.Draw("test", "Standard", 19));
}

static void CheckEmojiSearch()
{
    var face = EmojiSearch.Search("grinning face", 1).Single();
    Equal("😀", face.Emoji);
    Equal("grinning face", face.Name);
    Equal("U+1F600", EmojiSearch.Search("U+1F600", 1).Single().CodePoint);
    Equal("🏴󠁧󠁢󠁷󠁬󠁳󠁿", EmojiSearch.Search("welsh", 1).Single().Emoji);
    Equal(3, EmojiSearch.Search("flag", 3).Count);
    Throws<ArgumentOutOfRangeException>(() => EmojiSearch.Search("", 10));
    Throws<ArgumentOutOfRangeException>(() => EmojiSearch.Search("face", 31));
}

static void CheckDateTimeConversions()
{
    var value = DateTimeConverter.Convert("0", "unix_seconds");
    Equal("0", value.UnixSeconds);
    Equal("0", value.TimestampMs);
    Equal("00000000000000000", value.MongoObjectId);
    Equal("25569", value.Excel);
}

static void CheckUserAgentParsing()
{
    var chrome = UserAgentParser.Parse("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
    Equal("Chrome", chrome.Browser["name"]);
    Equal("120.0.0.0", chrome.Browser["version"]);
    Equal("Blink", chrome.Engine["name"]);
    Equal("Windows", chrome.Os["name"]);
    Equal("amd64", chrome.Cpu["architecture"]);
    Equal(0, UserAgentParser.Parse("curl/8.5.0").Browser.Count);
}

static void CheckPhoneNumberParsing()
{
    var us = PhoneNumberParser.Parse("+1 415 555 2671");
    Equal("US", us.CountryCode);
    Equal("United States", us.Country);
    Equal("+1", us.CallingCode);
    Equal(true, us.IsValid);
    Equal("FIXED_LINE_OR_MOBILE", us.Type);
    Equal("+14155552671", us.E164);
    Equal("+1 415 555 2671", us.International);
    Equal("tel:+14155552671", us.Rfc3966);

    var uk = PhoneNumberParser.Parse("020 7946 0018", "GB");
    Equal("GB", uk.CountryCode);
    Equal("+442079460018", uk.E164);
    Throws<ArgumentException>(() => PhoneNumberParser.Parse("020 7946 0018", "ZZ"));
}

static void CheckColorConversion()
{
    var green = ColorConverter.Convert("#1ea54c");
    Equal("rgb(30, 165, 76)", green.Rgb);
    Equal("#1ea54c", green.Hex);
    Equal("rebeccapurple", ColorConverter.Convert("rebeccapurple").Name);
    Equal("#ff0000", ColorConverter.Convert("device-cmyk(0% 100% 100% 0%)").Hex);
    Throws<FormatException>(() => ColorConverter.Convert("not-a-color"));
}

static void CheckMacVendorLookup()
{
    var cisco = MacVendorLookup.Lookup("00:00:0c:12:34:56");
    Equal("00000C", cisco.Prefix);
    Equal(true, cisco.Details?.StartsWith("Cisco Systems, Inc", StringComparison.Ordinal));
    Equal(null, MacVendorLookup.Lookup("02:00:00:00:00:00").Details);
    Throws<FormatException>(() => MacVendorLookup.Lookup("00:00:0"));
}

static void CheckQrCodes()
{
    Equal("WIFI:S:Example;T:WPA;P:sec\\;ret;H:true;", QrCodes.BuildWifiText(new WifiQrOptions("Example", "sec;ret", "WPA", null, true, false, "", null)));
    Equal("", QrCodes.BuildWifiText(new WifiQrOptions("Example", "secret", "WPA2-EAP", "PEAP", false, false, "", null)));
    var svg = QrCodes.GenerateSvg("https://killertools.net", "#112233", "#fefefe", "high");
    Equal(true, svg.StartsWith("<svg", StringComparison.Ordinal));
    Equal(true, svg.Contains("width=\"256\"", StringComparison.Ordinal));
    Equal(true, svg.Contains("#112233", StringComparison.Ordinal));
}

static void CheckCaseConversions()
{
    var values = CaseConverter.Convert("hello world");
    Equal(14, values.Count);
    Equal("helloWorld", values.Single(value => value.Label == "Camelcase").Value);
    Equal("Hello-World", values.Single(value => value.Label == "Headercase").Value);
}

static void CheckGroupPolicyLookup()
{
    Equal(633, GroupPolicyLookup.Count);
    Equal("Minimum Password Length", GroupPolicyLookup.Search("minimum password length", 1)[0].Name);
    Equal("Độ dài mật khẩu tối thiểu", ReferenceTranslations.Translate("group-policy-reference", "vi", "Minimum Password Length"));
}

static void CheckReferenceTranslations()
{
    string[] expectedLocales = ["bn", "cs", "de", "es", "fr", "hu", "it", "ja", "kk", "no", "pl", "pt", "ru", "tr", "uk", "vi", "zh", "zh-TW"];
    Equal(true, expectedLocales.SequenceEqual(ReferenceTranslations.SupportedLocales));
    foreach (var locale in expectedLocales)
    {
        ReferenceTranslations.Translate("http-status-codes", locale, "Not Found");
        ReferenceTranslations.Translate("group-policy-reference", locale, "Minimum Password Length");
    }

    Equal("Non trouvé", ReferenceTranslations.Translate("http-status-codes", "fr", "Not Found"));
    Equal("Xác thực SPF không thành công", ReferenceTranslations.Translate("exchange-ndr-lookup", "vi", "SPF Validation Failed"));
    Equal("Failed Logon", ReferenceTranslations.Translate("windows-event-lookup", "en", "Failed Logon"));
    Equal("Not Found", ReferenceTranslations.Translate("http-status-codes", "unknown", "Not Found"));
}

static void CheckKillerCatalogs()
{
    Equal("KillerPivot", KillerCatalogs.ListModules("pivot").Single()["name"]);
    const string catalog = "{\"BETA.ps1\":{\"name\":\"Beta\",\"description\":\"Second\"},\"ALPHA.ps1\":{\"name\":\"Alpha\",\"description\":\"First\"}}";
    using var client = new HttpClient(new StaticHttpHandler(catalog));
    var scripts = KillerCatalogs.ListScriptsAsync("first", client).GetAwaiter().GetResult();
    Equal(1, scripts.Count);
    Equal("ALPHA.ps1", scripts[0].Filename);
    Equal("https://raw.githubusercontent.com/SteveTheKiller/killer-scripts/main/ALPHA.ps1", scripts[0].DownloadUrl);
}

static void CheckPowerShellBuilder()
{
    var results = PowerShellBuilder.Search("ActiveDirectory", 20);
    Equal(true, results.Count > 0);
    Equal("Get-ADUser", PowerShellBuilder.Get("get-aduser").GetProperty("cmdlet").GetString());
    var parameters = System.Text.Json.JsonSerializer.SerializeToElement(new Dictionary<string, object> { ["Identity"] = "O'Brien", ["Properties"] = "DisplayName, mail" });
    var command = PowerShellBuilder.Build("Get-ADUser", parameters);
    Equal("Get-ADUser -Identity 'O''Brien' -Properties 'DisplayName','mail'", command.Command);
    Equal(false, command.RequiresAdmin);
    Throws<KeyNotFoundException>(() => PowerShellBuilder.Get("Not-A-Cmdlet"));
}

static void CheckMicrosoftReferenceLookups()
{
    var counts = MicrosoftReferenceLookups.Counts;
    Equal(206, counts.Skus);
    Equal(228, counts.Errors);
    Equal(292, counts.Events);
    Equal(113, counts.Ndrs);
    Equal("ERROR_ACCESS_DENIED", MicrosoftReferenceLookups.SearchErrors("0x00000005", 1)[0].Name);
    Equal(4625, MicrosoftReferenceLookups.SearchEvents("failed logon", 1)[0].Id);
    Equal("5.7.23", MicrosoftReferenceLookups.SearchNdrs("SPF Validation Failed", 1)[0].Code);
}

static void CheckPortProtocolLookup()
{
    Equal(392, PortProtocolLookup.Count);
    var result = PortProtocolLookup.Search("HTTPS", 10);
    Equal("HTTP", result[0].Service);
    Equal(80, result[0].Port.GetInt32());
}

static void CheckHttpStatusLookup()
{
    Equal(86, HttpStatusLookup.Count);
    var result = HttpStatusLookup.Search("not found", 10);
    Equal(404, result[0].Code);
    Equal("Not Found", result[0].Name);
}

static void CheckSafeRegex()
{
    var result = SafeRegex.Test("(cat)|(dog)", "Cat dog cat", true, true, false, true);
    Equal(3, result.Matches.Count);
    Equal("Cat", result.Matches[0].Match);
    Equal(null, result.Matches[0].Groups[1]);
}

static void CheckEmailHeaders()
{
    var parsed = EmailHeaderParser.Parse("From: Sender <sender@example.com>\r\nSender: relay@other.example\r\nAuthentication-Results: mx.example; spf=pass smtp.mailfrom=example.com; dkim=fail header.d=example.com\r\nX-MS-Exchange-Organization-SCL: 5");
    Equal(true, parsed.SenderMismatch);
    Equal("Spam", parsed.SclLabel);
    Equal(2, parsed.Auth.Count);
}

static void CheckEmailRecords()
{
    Equal("v=spf1 include:_spf.google.com ip4:192.0.2.0/24 ip6:2001:db8::/32 -all", EmailRecords.BuildSpf(["include:_spf.google.com"], ["192.0.2.0/24", "2001:db8::/32"], "-all"));
    Equal("v=DMARC1; p=quarantine; sp=reject; pct=50; rua=mailto:reports@example.com; adkim=s", EmailRecords.BuildDmarc("quarantine", "reject", 50, ["reports@example.com"], [], "s", ""));
}

static void CheckUrlAndCsv()
{
    using var document = System.Text.Json.JsonDocument.Parse("[{\"a\":1,\"b\":\"hello, world\"},{\"a\":2,\"c\":null}]");
    Equal("a,b,c\n1,\"hello, world\",\n2,,null", JsonCsv.Convert(document.RootElement));
    var url = UrlParser.Parse("https://user:pass@example.com:8443/a%20b?x=1&x=two+words#top");
    Equal("https:", url.Protocol);
    Equal("example.com", url.Hostname);
    Equal("8443", url.Port);
    Equal("two words", url.SearchParams[1][1]);
}

static void CheckStructuredJson()
{
    Equal("{\n   \"a\": 1,\n   \"b\": 2\n}", StructuredJson.Format("{b:2,a:1,}"));
    Equal("{\"text\":\"it's valid\",\"hex\":16}", StructuredJson.Minify("{/*comment*/text:'it\\'s valid',hex:0x10}"));
    var difference = System.Text.Json.JsonSerializer.SerializeToElement(StructuredJson.Diff("{ a: 1, same: true }", "{ a: 2, same: true }"));
    Equal("children-updated", difference.GetProperty("status").GetString());
    Equal(1, difference.GetProperty("children").GetArrayLength());
    Equal("updated", difference.GetProperty("children")[0].GetProperty("status").GetString());
    Throws<FormatException>(() => StructuredJson.Parse("{ broken:"));
}

static void CheckXmlJson()
{
    Equal("<a>\n  <b>text</b>\n</a>", XmlFormatter.Format("<a><b>text</b></a>"));
    Equal("<a>\n  <b>\n    text\n  </b>\n</a>", XmlFormatter.Format("<a><b>text</b></a>", 2, false));
    Equal("{\n  \"a\": {\n    \"_attributes\": {\n      \"x\": \"1\"\n    }\n  }\n}", XmlJsonConverter.XmlToJson("<a x=\"1\"/>"));
    Equal("<root>\n  <item id=\"1\">one</item>\n  <item>two</item>\n</root>", XmlJsonConverter.JsonToXml("{root:{item:[{_attributes:{id:'1'},_text:'one'},{_text:'two'}]}}"));
    Throws<System.Xml.XmlException>(() => XmlJsonConverter.XmlToJson("<root><"));
}

static void CheckYamlToml()
{
    Equal("{\n  \"a\": 1\n}", StructuredTextConverter.YamlToJson("a: 1"));
    Equal("{\n  \"a\": 1\n}", StructuredTextConverter.TomlToJson("a = 1"));
    Equal("a: 1\n", StructuredTextConverter.JsonToYaml("{a:1}"));
    Equal("a = 1", StructuredTextConverter.JsonToToml("{a:1}"));
    Equal("a: 1\nb: 2\n", StructuredTextConverter.FormatYaml("b: 2\na: 1", 2, true));
}

static void CheckMetaTags()
{
    var html = MetaTags.Generate("website", [new("title", "KillerTools"), new("url", "https://killertools.net")]);
    Equal(true, html.Contains("<meta property=\"og:title\" value=\"KillerTools\" />", StringComparison.Ordinal));
    Equal(true, html.Contains("<meta name=\"twitter:title\" value=\"KillerTools\" />", StringComparison.Ordinal));
    Throws<ArgumentException>(() => MetaTags.Generate("website", [new("unknown", "value")]));
}

static void CheckCronDescriptions()
{
    Equal("Every 5 minutes, every hour, every day", CronDescriptions.Describe("*/5 * * * *"));
    Throws<FormatException>(() => CronDescriptions.Describe("not a cron expression"));
}

static void CheckNetworkCalculations()
{
    var range = Ipv4Tools.CoverRange("192.168.1.1", "192.168.7.255");
    Equal("192.168.0.0/21", range.NewCidr);
    Equal(1791L, range.OldSize);

    var subnet = Ipv4Tools.CalculateSubnet("192.168.1.25/24");
    Equal("192.168.1.0/24", subnet.Cidr);
    Equal("192.168.1.255", subnet.BroadcastAddress);
    Equal(254L, subnet.UsableHosts);

    Equal("fd5b:9179:b4b9::/48", Ipv6Ula.Generate("00:11:22:33:44:55", 1700000000000).Ula48);
}

static void CheckDnsLookup()
{
    const string response = "{\"Status\":0,\"Answer\":[{\"name\":\"example.com.\",\"type\":1,\"TTL\":300,\"data\":\"192.0.2.1\"}]}";
    using var client = new HttpClient(new StaticHttpHandler(response));
    var result = DomainDnsLookup.LookupAsync("EXAMPLE.COM", "A", client).GetAwaiter().GetResult();
    Equal(0, result.Status);
    Equal(new DnsAnswer("example.com.", 1, 300, "192.0.2.1"), result.Answers.Single());
    Throws<FormatException>(() => DomainDnsLookup.LookupAsync("not a domain", "A", client).GetAwaiter().GetResult());
}

static void CheckRdapLookup()
{
    const string bootstrap = "{\"services\":[[[\"com\"],[\"https://rdap.example.net/\"]]]}";
    const string response = "{\"ldhName\":\"EXAMPLE.COM\",\"handle\":\"2336799_DOMAIN_COM-VRSN\",\"status\":[\"client delete prohibited\"],\"events\":[{\"eventAction\":\"registration\",\"eventDate\":\"1995-08-14T04:00:00Z\"}],\"nameservers\":[{\"ldhName\":\"A.IANA-SERVERS.NET\"}],\"secureDNS\":{\"delegationSigned\":true}}";
    using var client = new HttpClient(new RoutedHttpHandler(bootstrap, response));
    var result = DomainRdapLookup.LookupAsync("Example.COM", client).GetAwaiter().GetResult();
    Equal("EXAMPLE.COM", result.Domain);
    Equal("2336799_DOMAIN_COM-VRSN", result.Handle);
    Equal("client delete prohibited", result.Status.Single());
    Equal(new RdapEvent("registration", "1995-08-14T04:00:00Z"), result.Events.Single());
    Equal("A.IANA-SERVERS.NET", result.Nameservers.Single());
    Equal(true, result.DnssecSigned);
}

static void CheckCveLookup()
{
    const string response = "{\"totalResults\":1,\"vulnerabilities\":[{\"cve\":{\"id\":\"CVE-2025-53770\",\"published\":\"2025-07-19T00:00:00Z\",\"lastModified\":\"2025-07-20T00:00:00Z\",\"descriptions\":[{\"lang\":\"en\",\"value\":\"SharePoint vulnerability\"}],\"metrics\":{\"cvssMetricV31\":[{\"cvssData\":{\"baseScore\":9.8,\"baseSeverity\":\"CRITICAL\",\"vectorString\":\"CVSS:3.1/AV:N\"}}]},\"cisaExploitAdd\":\"2025-07-20\"}}]}";
    using var client = new HttpClient(new StaticHttpHandler(response));
    var result = CveLookup.LookupAsync("CVE-2025-53770", client).GetAwaiter().GetResult();
    Equal(1, result.TotalResults);
    Equal("CVE-2025-53770", result.Results.Single().Id);
    Equal("CRITICAL", result.Results.Single().Severity);
    Equal(true, result.Results.Single().CisaExploited);
    Equal("ToolShell", result.Results.Single().Chains.Single().Name);
    Equal("CVE-2025-53771", result.Results.Single().Chains.Single().OtherCves.Single());
}

static void CheckPortableUtilities()
{
    Equal(0d, Temperatures.Convert(32, "fahrenheit")["celsius"]);
    Equal("754", UnixPermissions.Calculate(new UnixPermissionSet(new(true, true, true), new(true, false, true), new(true, false, false))).Octal);
    Equal(true, SvgPlaceholder.Build(10, 20, 8, "#fff", "#000", true, "A&B").Contains("A&amp;B", StringComparison.Ordinal));
    Equal(8d, ExposureCalculators.WithNeutralDensity(1, 3));
    Equal(4d, ExposureCalculators.EquivalentShutter(1, 4, 8));
    Equal(true, ExposureCalculators.DepthOfField(50, 8, 3, "m", 0.03).NearMeters > 0);
}

static void CheckBase64Files()
{
    var directory = Path.Combine(Path.GetTempPath(), $"killertools-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var source = Path.Combine(directory, "source.bin");
        File.WriteAllBytes(source, [0, 1, 2, 254, 255]);
        var encoded = Base64Files.Encode(source);
        Equal("AAEC/v8=", encoded.Base64);
        Equal(5, encoded.ByteLength);

        var destination = Path.Combine(directory, "decoded.bin");
        var decoded = Base64Files.Decode(destination, encoded.Base64);
        Equal(5, decoded.ByteLength);
        Equal(true, File.ReadAllBytes(destination).SequenceEqual(new byte[] { 0, 1, 2, 254, 255 }));
        Throws<IOException>(() => Base64Files.Decode(destination, encoded.Base64));
        Throws<FormatException>(() => Base64Files.Decode(Path.Combine(directory, "invalid.bin"), "not base64"));
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void CheckOtp()
{
    const string secret = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";
    Equal("755224", OtpTools.GenerateCode(secret, 0));
    Equal(true, OtpTools.VerifyCode(secret, 0, "755224"));
    Equal(false, OtpTools.VerifyCode(secret, 0, "000000"));
    var generated = OtpTools.GenerateSecret("Killer Tools", "user@example.com");
    Equal(16, generated.Secret.Length);
    Equal(true, generated.Secret.All(character => "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567".Contains(character)));
    Equal(true, generated.Uri.StartsWith("otpauth://totp/Killer%20Tools:user%40example.com?issuer=Killer%20Tools&secret=", StringComparison.Ordinal));
}

static void CheckIdentifierGenerators()
{
    Equal("00000000-0000-0000-0000-000000000000", IdentifierGenerators.GenerateUuids("NIL", 1)[0]);
    Equal("9073926b-929f-31c2-abc9-fad77ae3e8eb", IdentifierGenerators.GenerateUuids("v3", 1, "example.com", "6ba7b810-9dad-11d1-80b4-00c04fd430c8")[0]);
    Equal("cfbff0d1-9375-5685-968c-48ce8b15ae17", IdentifierGenerators.GenerateUuids("v5", 1, "example.com", "6ba7b810-9dad-11d1-80b4-00c04fd430c8")[0]);
    Equal(true, Guid.TryParse(IdentifierGenerators.GenerateUuids("v4", 1)[0], out _));
    var ulid = IdentifierGenerators.GenerateUlids(1)[0];
    Equal(26, ulid.Length);
    Equal(true, ulid.All(character => "0123456789ABCDEFGHJKMNPQRSTVWXYZ".Contains(character)));
}

static void CheckJwt()
{
    var result = JwtParser.Decode("eyJhbGciOiJub25lIiwidHlwIjoiSldUIn0.eyJzdWIiOiJsb2NhbCIsInJvbGVzIjpbImFkbWluIl19.x");
    Equal("none", result.Header.GetProperty("alg").GetString());
    Equal("local", result.Payload.GetProperty("sub").GetString());
    Equal("Subject", result.PayloadClaims.Single(claim => (string)claim["claim"]! == "sub")["claimDescription"]);
    Equal(false, result.SignatureVerified);
}

static void CheckRsa()
{
    var pair = RsaKeyPairs.Generate(2048);
    Equal(true, pair.PublicKeyPem.Contains("BEGIN PUBLIC KEY", StringComparison.Ordinal));
    Equal(true, pair.PrivateKeyPem.Contains("BEGIN RSA PRIVATE KEY", StringComparison.Ordinal));
    using var rsa = System.Security.Cryptography.RSA.Create();
    rsa.ImportFromPem(pair.PrivateKeyPem);
    Equal(2048, rsa.KeySize);
}

static void CheckTextHashes()
{
    Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", TextHashes.Hash("abc", "SHA256"));
    Equal("18587dc2ea106b9a1563e32b3312421ca164c7f1f07bc922a9c83d77cea3a1e5d0c69910739025372dc14ac9642629379540c17e2a65b19d77aa511a9d00bb96", TextHashes.Hash("abc", "SHA3"));
    Equal("9c196e32dc0175f86f4b1cb89289d6619de6bee699e4c378e68309ed97a1a6ab", TextHashes.Hmac("abc", "key", "SHA256"));
    Equal("ungWv48Bz-pBQUDeXa4iI7ADYaOWF3qctBD_YfIAFa0", TextHashes.Hash("abc", "SHA256", "Base64url"));
}

static void CheckCryptoJsText()
{
    var vectors = new Dictionary<string, string>
    {
        ["AES"] = "U2FsdGVkX18d4wIIuTkHw/1yGxTmuPls/WkrkG1+dpA=",
        ["TripleDES"] = "U2FsdGVkX1/lW75QiWf2fn5DMExkn3XhTGzHyHbKIpo=",
        ["Rabbit"] = "U2FsdGVkX188DHu+QI2qI5bnjxYsRt4F",
        ["RC4"] = "U2FsdGVkX1/WxAb6+S0hDtDfUGbKgCY6",
    };
    foreach (var vector in vectors)
    {
        Equal("hello π", CryptoJsText.Decrypt(vector.Value, "secret", vector.Key));
        string encrypted = CryptoJsText.Encrypt("round trip π", "secret", vector.Key);
        Equal("round trip π", CryptoJsText.Decrypt(encrypted, "secret", vector.Key));
    }
}

static void CheckBcrypt()
{
    const string bcryptJsHash = "$2b$04$abcdefghijklmnopqrstuuSOK8qq7pwSzIsE/gr5lck387Zp7ucXu";
    Equal(true, PasswordBcrypt.Verify("private", bcryptJsHash));
    Equal(false, PasswordBcrypt.Verify("wrong", bcryptJsHash));
    var hash = PasswordBcrypt.Hash("private", 4);
    Equal(true, PasswordBcrypt.Verify("private", hash));
}

static void CheckPasswordStrength()
{
    Equal(26, PasswordStrength.GetCharsetLength("abcdefghijklmnopqrstuvwxyz"));
    Equal(94, PasswordStrength.GetCharsetLength("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_"));
    var result = PasswordStrength.Analyze("Secret123!");
    Equal(94, result.CharsetLength);
    Equal(10, result.PasswordLength);
    Equal(true, result.Entropy > 0);
    Equal(true, result.CrackDurationFormatted.Length > 0);
}

static void CheckPasswordGeneration()
{
    Equal(7776, PasswordGenerator.WordCount);
    var random = PasswordGenerator.GenerateRandom(32, true, true, true, true, true, true);
    Equal(32, random.Length);
    Equal(true, random.Any(char.IsUpper));
    Equal(true, random.Any(char.IsLower));
    Equal(true, random.Any(char.IsDigit));
    Equal(true, random.Any(character => "!@#$%^&*()-_=+<>?".Contains(character)));
    Equal(false, random.Any(character => "0O1lI|`'\".,;:{}[]()\\/".Contains(character)));
    var passphrase = PasswordGenerator.GeneratePassphrase(6, "-", true, true).Split('-');
    Equal(7, passphrase.Length);
    Equal(true, passphrase.Take(6).All(word => char.IsUpper(word[0])));
    Equal(true, passphrase[6].Length == 2 && passphrase[6].All(char.IsDigit));
    Equal(true, System.Text.RegularExpressions.Regex.IsMatch(PasswordGenerator.GeneratePronounceable(20, true), "^[bcdfghjkmnpqrstvwxz][aeiouy](?:[bcdfghjkmnpqrstvwxz][aeiouy]){8}[0-9]{2}$"));
    Equal(true, System.Text.RegularExpressions.Regex.IsMatch(PasswordGenerator.GenerateFormatted(20, "hex"), "^[0-9a-f]{20}$"));
    Equal(true, Guid.TryParse(PasswordGenerator.GenerateFormatted(20, "uuid"), out _));
}

static void CheckBip39()
{
    var expected = new Dictionary<string, string>
    {
        ["English"] = "abandon abandon abandon abandon abandon able",
        ["Czech"] = "abdikace abdikace abdikace abdikace abdikace adresa",
        ["French"] = "abaisser abaisser abaisser abaisser abaisser abdiquer",
        ["Italian"] = "abaco abaco abaco abaco abaco abbinato",
        ["Japanese"] = "あいこくしん　あいこくしん　あいこくしん　あいこくしん　あいこくしん　あいだ",
        ["Korean"] = "가격 가격 가격 가격 가격 가난",
        ["Portuguese"] = "abacate abacate abacate abacate abacate abalar",
        ["Spanish"] = "ábaco ábaco ábaco ábaco ábaco abeja",
        ["Chinese simplified"] = "的 的 的 的 的 是",
        ["Chinese traditional"] = "的 的 的 的 的 是",
    };
    Equal(10, Bip39.Languages.Count);
    foreach (var (language, mnemonic) in expected)
    {
        Equal(mnemonic, Bip39.EntropyToMnemonic("0000000000000000", language));
        Equal("0000000000000000", Bip39.MnemonicToEntropy(mnemonic, language));
    }
    Equal(32, Bip39.GenerateEntropy().Length);
}

static void CheckBoundedMath()
{
    Equal(14d, BoundedMath.Evaluate("2 + 3 * 4"));
    Equal(14d, BoundedMath.Evaluate("sqrt(81) + max(2, 5)"));
    Equal(1d, BoundedMath.Evaluate("sin(pi / 2)"));
    Equal(1024d, BoundedMath.Evaluate("2^10"));
    Throws<FormatException>(() => BoundedMath.Evaluate("2^13"));
    Throws<FormatException>(() => BoundedMath.Evaluate("unknown(1)"));
}

static void CheckFilmReciprocity()
{
    Equal(41, FilmReciprocity.List().Count);
    Equal("HP5 Plus", FilmReciprocity.List()[0].Name);
    var adjusted = FilmReciprocity.Calculate("ilford-hp5", 10);
    Equal(System.Math.Pow(10, 1.31), adjusted.AdjustedSeconds);
    Equal(false, adjusted.NoFailure);
    var acros = FilmReciprocity.Calculate("fuji-acros100ii", 120);
    Equal(120d, acros.AdjustedSeconds);
    Equal(true, acros.NoFailure);
}

static void CheckFilmDevelopment()
{
    Equal(10, FilmDevelopment.Developers.Count);
    Equal(74, FilmDevelopment.Films.Count);
    Equal("D-76", FilmDevelopment.Developers.Single(item => item.Id == "d76").Name);
    var result = FilmDevelopment.Calculate("Ilford HP5 Plus", "d76");
    Equal(390d, result.BaseSeconds);
    Equal(390d, result.FinalSeconds);
    Equal("6m 30s", result.DisplayTime);
    Equal(500d, result.DevMl);
    Equal(0d, result.WaterMl);
    Equal("bw", result.Process);
    Throws<KeyNotFoundException>(() => FilmDevelopment.Calculate("Unknown", "d76"));
}

static void CheckHtmlAndDiff()
{
    Equal("&lt;a title=&quot;x&quot;&gt;Tom &amp; Jerry&#39;s&lt;/a&gt;", HtmlEntities.Escape("<a title=\"x\">Tom & Jerry's</a>"));
    Equal("<a title=\"x\">Tom & Jerry's</a>", HtmlEntities.Unescape("&lt;a title=&quot;x&quot;&gt;Tom &amp; Jerry&#39;s&lt;/a&gt;"));

    var difference = TextDiff.Compare("one\ntwo\nthree", "one\nnew\nthree");
    Equal(false, difference.Equal);
    Equal(1, difference.Added);
    Equal(1, difference.Removed);
    Equal(new TextDiffLine("context", "one"), difference.Lines[0]);
    Equal(new TextDiffLine("add", "new"), difference.Lines[1]);
    Equal(new TextDiffLine("remove", "two"), difference.Lines[2]);
}

static void CheckMarkdown()
{
    Equal("<h1>Hello</h1>\n<ul>\n<li>one</li>\n<li>two with <strong>bold</strong></li>\n</ul>\n", MarkdownHtml.Convert("# Hello\n\n- one\n- two with **bold**"));
    Equal("<p><a href=\"https://example.com\">link</a></p>\n<blockquote>\n<p>quoted <code>code</code></p>\n</blockquote>\n", MarkdownHtml.Convert("[link](https://example.com)\n\n> quoted `code`"));
}

static void CheckGifSearch()
{
    const string response = "{\"data\":[{\"id\":\"abc\",\"title\":\"Test GIF\",\"images\":{\"fixed_height_small\":{\"url\":\"https://images.example/preview.gif\"},\"original\":{\"url\":\"https://images.example/full.gif\"}}}]}";
    using var client = new HttpClient(new StaticHttpHandler(response));
    Equal(new GifSearchResult("abc", "Test GIF", "https://images.example/preview.gif", "https://images.example/full.gif"), GifSearch.SearchAsync("test", 1, client).GetAwaiter().GetResult().Single());
}

static void CheckTextConversions()
{
    Equal("aMOp", Base64Text.Encode("hé"));
    Equal("hé", Base64Text.Decode("aMOp"));
    Equal("01000001", AsciiBinary.Encode("A"));
    Equal("A", AsciiBinary.Decode("01000001"));
    Equal("MCMXCIX", RomanNumerals.FromArabic(1999));
    Equal(1999, RomanNumerals.ToArabic("MCMXCIX"));
    Equal("Sierra Oscar Sierra", NatoAlphabet.Convert("SOS"));
    Equal("ff", IntegerBases.Convert("255", 10, 16));
    Equal("100000000000000000000000000000000", IntegerBases.Convert("100000000", 16, 2));
}

static void CheckTextStatistics()
{
    Equal(new TextStatisticsResult(0, 0, 0, 0), TextStatistics.Calculate(string.Empty));
    Equal(new TextStatisticsResult(11, 2, 1, 11), TextStatistics.Calculate("hello world"));
    Equal(new TextStatisticsResult(4, 2, 2, 5), TextStatistics.Calculate("a\r\né"));
    Equal(new TextStatisticsResult(3, 3, 1, 3), TextStatistics.Calculate(" a "));
}

static void CheckUnicodeEntities()
{
    Equal("&#65;&#38;", UnicodeEntities.Encode("A&"));
    Equal("A&", UnicodeEntities.Decode("&#65;&#38;"));
    Equal("text &#999999;", UnicodeEntities.Decode("text &#999999;"));
}

static void CheckPercentages()
{
    Equal("50", Percentages.Of(25, 200));
    Equal("25%", Percentages.Ratio(1, 4));
    Equal("+20%", Percentages.Change(10, 12));
    Equal("-20%", Percentages.Change(10, 8));
    Equal(string.Empty, Percentages.Ratio(1, 0));
}

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected {expected}, received {actual}.");
    }
}

static void Throws<T>(Action action) where T : Exception
{
    try
    {
        action();
    }
    catch (T)
    {
        return;
    }
    throw new InvalidOperationException($"Expected {typeof(T).Name}.");
}

sealed class StaticHttpHandler(string content) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
    {
        Content = new StringContent(content),
    });
}

sealed class RoutedHttpHandler(string bootstrap, string lookup) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
    {
        Content = new StringContent(request.RequestUri?.Host == "data.iana.org" ? bootstrap : lookup),
    });
}
