using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace KillerTools.Engine.Reference;

public sealed record HttpStatusEntry(string Category, int Code, string Name, string Description, string Type);

public static class HttpStatusLookup
{
    private sealed record Group(string Category, HttpCode[] Codes);
    private sealed record HttpCode(int Code, string Name, string Description, string Type);

    private static readonly Lazy<HttpStatusEntry[]> Entries = new(Load);

    public static int Count => Entries.Value.Length;

    public static IReadOnlyList<HttpStatusEntry> Search(string query, int limit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        var needle = query.ToLowerInvariant();
        return Entries.Value.Where(entry =>
                entry.Code.ToString(CultureInfo.InvariantCulture).Contains(needle, StringComparison.OrdinalIgnoreCase)
                || entry.Name.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || entry.Description.Contains(needle, StringComparison.OrdinalIgnoreCase))
            .Take(limit).ToArray();
    }

    private static HttpStatusEntry[] Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("KillerTools.Engine.Data.http-status.json")
            ?? throw new InvalidOperationException("The HTTP status catalog is missing.");
        var groups = JsonSerializer.Deserialize<Group[]>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("The HTTP status catalog is invalid.");
        return groups.SelectMany(group => group.Codes.Select(code => new HttpStatusEntry(group.Category, code.Code, code.Name, code.Description, code.Type))).ToArray();
    }
}
