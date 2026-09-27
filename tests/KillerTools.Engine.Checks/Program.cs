using KillerTools.Engine.Math;
using KillerTools.Engine.Numbers;
using KillerTools.Engine.Text;
using KillerTools.Engine.Documents;
using KillerTools.Engine.SystemTools;
using KillerTools.Engine.Photography;
using KillerTools.Engine.Network;
using KillerTools.Engine.Reference;

var checks = new (string Name, Action Run)[]
{
    ("text statistics", CheckTextStatistics),
    ("Unicode entities", CheckUnicodeEntities),
    ("percentages", CheckPercentages),
    ("text conversions", CheckTextConversions),
    ("HTML entities and text diff", CheckHtmlAndDiff),
    ("IP network calculations", CheckNetworkCalculations),
    ("URL and CSV conversions", CheckUrlAndCsv),
    ("email records", CheckEmailRecords),
    ("email headers", CheckEmailHeaders),
    ("safe regular expressions", CheckSafeRegex),
    ("HTTP status lookup", CheckHttpStatusLookup),
    ("port and protocol lookup", CheckPortProtocolLookup),
    ("Microsoft reference lookups", CheckMicrosoftReferenceLookups),
    ("reference translations", CheckReferenceTranslations),
    ("portable utilities", CheckPortableUtilities),
};

foreach (var check in checks)
{
    check.Run();
    Console.WriteLine($"PASS {check.Name}");
}

static void CheckReferenceTranslations()
{
    Equal(18, ReferenceTranslations.SupportedLocales.Count);
    Equal("Non trouvé", ReferenceTranslations.Translate("http-status-codes", "fr", "Not Found"));
    Equal("Xác thực SPF không thành công", ReferenceTranslations.Translate("exchange-ndr-lookup", "vi", "SPF Validation Failed"));
    Equal("Failed Logon", ReferenceTranslations.Translate("windows-event-lookup", "en", "Failed Logon"));
    Equal("Not Found", ReferenceTranslations.Translate("http-status-codes", "unknown", "Not Found"));
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

static void CheckPortableUtilities()
{
    Equal(0d, Temperatures.Convert(32, "fahrenheit")["celsius"]);
    Equal("754", UnixPermissions.Calculate(new UnixPermissionSet(new(true, true, true), new(true, false, true), new(true, false, false))).Octal);
    Equal(true, SvgPlaceholder.Build(10, 20, 8, "#fff", "#000", true, "A&B").Contains("A&amp;B", StringComparison.Ordinal));
    Equal(8d, ExposureCalculators.WithNeutralDensity(1, 3));
    Equal(4d, ExposureCalculators.EquivalentShutter(1, 4, 8));
    Equal(true, ExposureCalculators.DepthOfField(50, 8, 3, "m", 0.03).NearMeters > 0);
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
