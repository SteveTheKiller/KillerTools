using System.Text.Json;
using System.Text.RegularExpressions;

namespace KillerTools.Engine.Network;

public sealed record DnsAnswer(string? Name, int? Type, int? Ttl, string? Data);
public sealed record DnsLookupResult(int? Status, IReadOnlyList<DnsAnswer> Answers);

public static partial class DomainDnsLookup
{
    private static readonly HashSet<string> RecordTypes = ["A", "AAAA", "MX", "TXT", "CNAME", "NS", "CAA", "SRV"];
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(8) };

    public static async Task<DnsLookupResult> LookupAsync(string name, string type = "A", HttpClient? client = null, CancellationToken cancellationToken = default)
    {
        name = name.Trim().ToLowerInvariant();
        if (!DnsName().IsMatch(name)) throw new FormatException("Invalid DNS name.");
        if (!RecordTypes.Contains(type)) throw new ArgumentException("Unknown DNS record type.", nameof(type));
        var url = $"https://cloudflare-dns.com/dns-query?name={Uri.EscapeDataString(name)}&type={type}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.ParseAdd("application/dns-json");
        using var response = await (client ?? Client).SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await ReadBoundedJsonAsync(stream, 32_768, cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;
        int? status = root.TryGetProperty("Status", out var statusElement) && statusElement.TryGetInt32(out var statusValue) ? statusValue : null;
        var answers = new List<DnsAnswer>();
        if (root.TryGetProperty("Answer", out var answerElements) && answerElements.ValueKind == JsonValueKind.Array)
        {
            foreach (var answer in answerElements.EnumerateArray().Take(30))
            {
                answers.Add(new(GetString(answer, "name"), GetInteger(answer, "type"), GetInteger(answer, "TTL"), Truncate(GetString(answer, "data"), 4096)));
            }
        }
        return new(status, answers);
    }

    private static async Task<JsonDocument> ReadBoundedJsonAsync(Stream stream, int maxBytes, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        while (true)
        {
            var read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            if (buffer.Length + read > maxBytes) throw new InvalidDataException("DNS response is too large.");
            buffer.Write(chunk, 0, read);
        }
        buffer.Position = 0;
        return await JsonDocument.ParseAsync(buffer, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static string? GetString(JsonElement value, string name) => value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString() : null;
    private static int? GetInteger(JsonElement value, string name) => value.TryGetProperty(name, out var property) && property.TryGetInt32(out var number) ? number : null;
    private static string? Truncate(string? value, int length) => value is not null && value.Length > length ? value[..length] : value;

    [GeneratedRegex("^(?:[a-z0-9_](?:[a-z0-9_-]{0,61}[a-z0-9_])?\\.)+[a-z]{2,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex DnsName();
}
