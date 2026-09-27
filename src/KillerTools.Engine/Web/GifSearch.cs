using System.Text.Json;

namespace KillerTools.Engine.Web;

public sealed record GifSearchResult(string Id, string Title, string PreviewUrl, string FullUrl);

public static class GifSearch
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(8) };

    public static async Task<IReadOnlyList<GifSearchResult>> SearchAsync(string query, int limit = 10, HttpClient? client = null, CancellationToken cancellationToken = default)
    {
        query = query.Trim();
        if (query.Length is < 1 or > 80 || limit is < 1 or > 20) throw new ArgumentOutOfRangeException();
        var uri = new Uri($"https://gif-proxy.cst-498.workers.dev/?q={Uri.EscapeDataString(query)}&limit={limit}");
        using var response = await (client ?? Client).GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await ReadBoundedJsonAsync(stream, cancellationToken).ConfigureAwait(false);
        if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array) throw new InvalidDataException();
        var results = new List<GifSearchResult>();
        foreach (var item in data.EnumerateArray().Take(limit))
        {
            var id = GetString(item, "id") ?? throw new InvalidDataException();
            if (!item.TryGetProperty("images", out var images) || images.ValueKind != JsonValueKind.Object) throw new InvalidDataException();
            var preview = ImageUrl(images, "fixed_height_small") ?? ImageUrl(images, "fixed_height");
            var full = ImageUrl(images, "original");
            if (!IsHttps(preview) || !IsHttps(full)) throw new InvalidDataException();
            var title = GetString(item, "title") ?? "GIF";
            results.Add(new(id, title.Length > 200 ? title[..200] : title, preview!, full!));
        }
        return results;
    }

    private static string? ImageUrl(JsonElement images, string name) => images.TryGetProperty(name, out var image) && image.ValueKind == JsonValueKind.Object ? GetString(image, "url") : null;
    private static string? GetString(JsonElement value, string name) => value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString() : null;
    private static bool IsHttps(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;

    private static async Task<JsonDocument> ReadBoundedJsonAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        while (true)
        {
            var read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            if (buffer.Length + read > 131_072) throw new InvalidDataException();
            buffer.Write(chunk, 0, read);
        }
        buffer.Position = 0;
        return await JsonDocument.ParseAsync(buffer, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
