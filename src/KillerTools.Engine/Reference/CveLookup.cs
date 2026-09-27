using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace KillerTools.Engine.Reference;

public sealed record CveChain([property: JsonPropertyName("name")] string Name, [property: JsonPropertyName("otherCves")] IReadOnlyList<string> OtherCves);
public sealed record CveSummary(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("published")] string? Published,
    [property: JsonPropertyName("lastModified")] string? LastModified,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("severity")] string? Severity,
    [property: JsonPropertyName("score")] double? Score,
    [property: JsonPropertyName("vector")] string? Vector,
    [property: JsonPropertyName("cisaExploited")] bool CisaExploited,
    [property: JsonPropertyName("chains")] IReadOnlyList<CveChain> Chains,
    [property: JsonPropertyName("url")] string Url);
public sealed record CveLookupResult(
    [property: JsonPropertyName("totalResults")] int TotalResults,
    [property: JsonPropertyName("results")] IReadOnlyList<CveSummary> Results,
    [property: JsonPropertyName("aliasIds")] IReadOnlyList<string>? AliasIds);

public static partial class CveLookup
{
    private const string Proxy = "https://nvd-proxy.cst-498.workers.dev/";
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(15) };
    private static readonly IReadOnlyDictionary<string, string[]> Aliases = LoadAliases();
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<CveChain>> Chains = LoadChains();
    private static readonly Dictionary<string, (DateTimeOffset Until, JsonElement Value)> Cache = new(StringComparer.Ordinal);
    private static readonly object CacheLock = new();

    public static async Task<CveLookupResult> LookupAsync(string query, HttpClient? client = null, CancellationToken cancellationToken = default)
    {
        query = query.Trim();
        if (query.Length is < 2 or > 80) throw new ArgumentOutOfRangeException(nameof(query));
        Aliases.TryGetValue(query.ToLowerInvariant(), out var aliasIds);
        JsonElement[] vulnerabilities;
        int totalResults;
        if (aliasIds is not null)
        {
            var pages = await Task.WhenAll(aliasIds.Take(10).Select(id => FetchAsync(id, true, client ?? Client, cancellationToken))).ConfigureAwait(false);
            vulnerabilities = pages.SelectMany(Vulnerabilities).ToArray();
            totalResults = vulnerabilities.Length;
        }
        else
        {
            var page = await FetchAsync(query, CveIdRegex().IsMatch(query), client ?? Client, cancellationToken).ConfigureAwait(false);
            vulnerabilities = Vulnerabilities(page).ToArray();
            totalResults = page.TryGetProperty("totalResults", out var total) && total.TryGetInt32(out var count) ? count : vulnerabilities.Length;
        }
        var results = vulnerabilities.Take(10).Select(Parse).ToArray();
        return new CveLookupResult(totalResults, results, aliasIds);
    }

    private static async Task<JsonElement> FetchAsync(string query, bool exact, HttpClient client, CancellationToken cancellationToken)
    {
        var key = $"{(exact ? "id" : "keyword")}:{query}";
        lock (CacheLock) if (Cache.TryGetValue(key, out var cached) && cached.Until > DateTimeOffset.UtcNow) return cached.Value;
        var url = exact
            ? $"{Proxy}?cveId={Uri.EscapeDataString(query.ToUpperInvariant())}"
            : $"{Proxy}?keywordSearch={Uri.EscapeDataString(query)}&keywordExactMatch=&resultsPerPage=10";
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await ReadBoundedAsync(stream, cancellationToken).ConfigureAwait(false);
        if (!document.RootElement.TryGetProperty("vulnerabilities", out var values) || values.ValueKind != JsonValueKind.Array) throw new InvalidDataException("Invalid CVE response");
        var root = document.RootElement.Clone();
        lock (CacheLock)
        {
            if (Cache.Count >= 64) Cache.Remove(Cache.Keys.First());
            Cache[key] = (DateTimeOffset.UtcNow.AddMinutes(5), root);
        }
        return root;
    }

    private static IEnumerable<JsonElement> Vulnerabilities(JsonElement page) => page.GetProperty("vulnerabilities").EnumerateArray().Select(item => item.Clone());

    private static CveSummary Parse(JsonElement item)
    {
        if (!item.TryGetProperty("cve", out var cve) || !cve.TryGetProperty("id", out var idValue) || idValue.ValueKind != JsonValueKind.String) throw new InvalidDataException("Invalid CVE");
        var id = idValue.GetString()!;
        var description = cve.TryGetProperty("descriptions", out var descriptions) && descriptions.ValueKind == JsonValueKind.Array
            ? descriptions.EnumerateArray().FirstOrDefault(value => String(value, "lang") == "en")
            : default;
        var descriptionText = description.ValueKind == JsonValueKind.Object ? String(description, "value") ?? "" : "";
        if (descriptionText.Length > 1500) descriptionText = descriptionText[..1500];
        JsonElement score = default;
        if (cve.TryGetProperty("metrics", out var metrics) && metrics.ValueKind == JsonValueKind.Object)
        {
            foreach (var name in new[] { "cvssMetricV40", "cvssMetricV31", "cvssMetricV30", "cvssMetricV2" })
            {
                if (!metrics.TryGetProperty(name, out var metric) || metric.ValueKind != JsonValueKind.Array || metric.GetArrayLength() == 0) continue;
                var first = metric[0];
                if (first.TryGetProperty("cvssData", out score) && score.ValueKind == JsonValueKind.Object) break;
            }
        }
        var severity = score.ValueKind == JsonValueKind.Object ? String(score, "baseSeverity") : null;
        var vector = score.ValueKind == JsonValueKind.Object ? String(score, "vectorString") : null;
        var baseScore = score.ValueKind == JsonValueKind.Object && score.TryGetProperty("baseScore", out var number) && number.TryGetDouble(out var value) ? value : (double?)null;
        return new CveSummary(id, String(cve, "published"), String(cve, "lastModified"), descriptionText, severity, baseScore, vector, cve.TryGetProperty("cisaExploitAdd", out _), Chains.GetValueOrDefault(id.ToUpperInvariant()) ?? [], $"https://nvd.nist.gov/vuln/detail/{id}");
    }

    private static string? String(JsonElement value, string name) => value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString() : null;
    private static async Task<JsonDocument> ReadBoundedAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        while (true)
        {
            var read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            if (buffer.Length + read > 262_144) throw new InvalidDataException("CVE response too large");
            buffer.Write(chunk, 0, read);
        }
        buffer.Position = 0;
        return await JsonDocument.ParseAsync(buffer, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static IReadOnlyDictionary<string, string[]> LoadAliases()
    {
        using var stream = Resource("cve-aliases.json");
        return JsonSerializer.Deserialize<Dictionary<string, string[]>>(stream) ?? throw new InvalidDataException("CVE aliases are invalid.");
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<CveChain>> LoadChains()
    {
        using var stream = Resource("cve-chains.json");
        using var document = JsonDocument.Parse(stream);
        var result = new Dictionary<string, List<CveChain>>(StringComparer.OrdinalIgnoreCase);
        foreach (var chain in document.RootElement.EnumerateArray())
        {
            var name = chain.GetProperty("name").GetString()!;
            var ids = chain.GetProperty("cves").EnumerateArray().Select(value => value.GetString()!.ToUpperInvariant()).ToArray();
            foreach (var id in ids) (result.TryGetValue(id, out var list) ? list : result[id] = []).Add(new CveChain(name, ids.Where(other => other != id).ToArray()));
        }
        return result.ToDictionary(item => item.Key, item => (IReadOnlyList<CveChain>)item.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static Stream Resource(string name) => Assembly.GetExecutingAssembly().GetManifestResourceStream($"KillerTools.Engine.Data.{name}") ?? throw new InvalidOperationException($"{name} is missing.");
    [GeneratedRegex("^CVE-\\d{4}-\\d{4,}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex CveIdRegex();
}
