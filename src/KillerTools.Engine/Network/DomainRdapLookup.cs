using System.Text.Json;
using System.Text.RegularExpressions;

namespace KillerTools.Engine.Network;

public sealed record RdapEvent(string? EventAction, string? EventDate);
public sealed record RdapLookupResult(string Domain, string? Handle, IReadOnlyList<string> Status, IReadOnlyList<RdapEvent> Events, IReadOnlyList<string> Nameservers, bool? DnssecSigned);

public static partial class DomainRdapLookup
{
    private static readonly HttpClient Client = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(10) };
    private static readonly SemaphoreSlim BootstrapLock = new(1, 1);
    private static IReadOnlyDictionary<string, Uri>? bootstrap;
    private static DateTimeOffset bootstrapExpires;

    public static async Task<RdapLookupResult> LookupAsync(string domain, HttpClient? client = null, CancellationToken cancellationToken = default)
    {
        domain = domain.Trim().ToLowerInvariant();
        if (!DomainName().IsMatch(domain)) throw new FormatException("Invalid domain name.");
        var activeClient = client ?? Client;
        var endpoints = await GetBootstrapAsync(activeClient, cancellationToken).ConfigureAwait(false);
        var tld = domain[(domain.LastIndexOf('.') + 1)..];
        if (!endpoints.TryGetValue(tld, out var endpoint)) throw new InvalidOperationException("RDAP registry unavailable.");
        var requestUri = new Uri(endpoint, $"domain/{Uri.EscapeDataString(domain)}");
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Accept.ParseAdd("application/rdap+json");
        using var response = await activeClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await ReadBoundedJsonAsync(stream, 131_072, cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;
        return new(
            GetString(root, "ldhName") ?? domain,
            GetString(root, "handle"),
            GetStrings(root, "status", 20),
            GetEvents(root),
            GetNameservers(root),
            GetBoolean(root, "secureDNS", "delegationSigned"));
    }

    private static async Task<IReadOnlyDictionary<string, Uri>> GetBootstrapAsync(HttpClient client, CancellationToken cancellationToken)
    {
        if (bootstrap is not null && DateTimeOffset.UtcNow < bootstrapExpires) return bootstrap;
        await BootstrapLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (bootstrap is not null && DateTimeOffset.UtcNow < bootstrapExpires) return bootstrap;
            using var response = await client.GetAsync("https://data.iana.org/rdap/dns.json", HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await ReadBoundedJsonAsync(stream, 262_144, cancellationToken).ConfigureAwait(false);
            var next = new Dictionary<string, Uri>(StringComparer.OrdinalIgnoreCase);
            if (document.RootElement.TryGetProperty("services", out var services) && services.ValueKind == JsonValueKind.Array)
            {
                foreach (var service in services.EnumerateArray())
                {
                    if (service.ValueKind != JsonValueKind.Array || service.GetArrayLength() < 2) continue;
                    var endpoint = service[1].EnumerateArray().Select(value => value.GetString()).Select(ValidEndpoint).FirstOrDefault(value => value is not null);
                    if (endpoint is null) continue;
                    foreach (var suffix in service[0].EnumerateArray().Select(value => value.GetString()).Where(value => !string.IsNullOrWhiteSpace(value))) next[suffix!.ToLowerInvariant()] = endpoint;
                }
            }
            bootstrap = next;
            bootstrapExpires = DateTimeOffset.UtcNow.AddDays(1);
            return bootstrap;
        }
        finally
        {
            BootstrapLock.Release();
        }
    }

    private static Uri? ValidEndpoint(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var endpoint) || endpoint.Scheme != Uri.UriSchemeHttps || !endpoint.IsDefaultPort || !EndpointHost().IsMatch(endpoint.Host)) return null;
        if (BlockedHost().IsMatch(endpoint.Host)) return null;
        return endpoint.AbsoluteUri.EndsWith('/') ? endpoint : new Uri(endpoint.AbsoluteUri + "/");
    }

    private static async Task<JsonDocument> ReadBoundedJsonAsync(Stream stream, int maxBytes, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        while (true)
        {
            var read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            if (buffer.Length + read > maxBytes) throw new InvalidDataException("RDAP response is too large.");
            buffer.Write(chunk, 0, read);
        }
        buffer.Position = 0;
        return await JsonDocument.ParseAsync(buffer, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static IReadOnlyList<string> GetStrings(JsonElement root, string name, int limit) => root.TryGetProperty(name, out var values) && values.ValueKind == JsonValueKind.Array
        ? values.EnumerateArray().Take(limit).Where(value => value.ValueKind == JsonValueKind.String).Select(value => value.GetString()!).ToArray()
        : [];

    private static IReadOnlyList<RdapEvent> GetEvents(JsonElement root) => root.TryGetProperty("events", out var values) && values.ValueKind == JsonValueKind.Array
        ? values.EnumerateArray().Take(20).Select(value => new RdapEvent(GetString(value, "eventAction"), GetString(value, "eventDate"))).ToArray()
        : [];

    private static IReadOnlyList<string> GetNameservers(JsonElement root) => root.TryGetProperty("nameservers", out var values) && values.ValueKind == JsonValueKind.Array
        ? values.EnumerateArray().Take(20).Select(value => GetString(value, "ldhName")).Where(value => value is not null).Select(value => value!).ToArray()
        : [];

    private static string? GetString(JsonElement value, string name) => value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString() : null;
    private static bool? GetBoolean(JsonElement value, string parent, string name) => value.TryGetProperty(parent, out var container) && container.ValueKind == JsonValueKind.Object && container.TryGetProperty(name, out var property) && (property.ValueKind == JsonValueKind.True || property.ValueKind == JsonValueKind.False) ? property.GetBoolean() : null;

    [GeneratedRegex("^(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\\.)+[a-z]{2,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex DomainName();
    [GeneratedRegex("^(?:[a-z0-9-]+\\.)+[a-z]{2,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex EndpointHost();
    [GeneratedRegex("\\.(?:localhost|local|internal|test|invalid)$", RegexOptions.CultureInvariant)]
    private static partial Regex BlockedHost();
}
