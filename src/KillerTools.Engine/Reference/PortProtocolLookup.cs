using System.Globalization;
using System.Text.Json;

namespace KillerTools.Engine.Reference;

public sealed record PortProtocolEntry(string Category, JsonElement Port, string Protocol, string Service, string Description, bool Dangerous, string? Notes);

public static class PortProtocolLookup
{
    private sealed record Group(string Category, PortData[] Ports);
    private sealed record PortData(JsonElement Port, string Protocol, string Service, string Description, bool Dangerous, string? Notes);
    private static readonly Lazy<PortProtocolEntry[]> Entries = new(Load);

    public static int Count => Entries.Value.Length;

    public static IReadOnlyList<PortProtocolEntry> Search(string query, int limit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        return Entries.Value.Where(entry => PortText(entry.Port).Contains(query, StringComparison.OrdinalIgnoreCase)
                || entry.Protocol.Contains(query, StringComparison.OrdinalIgnoreCase)
                || entry.Service.Contains(query, StringComparison.OrdinalIgnoreCase)
                || entry.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(limit).ToArray();
    }

    private static string PortText(JsonElement port) => port.ValueKind == JsonValueKind.String ? port.GetString()! : port.GetInt32().ToString(CultureInfo.InvariantCulture);

    private static PortProtocolEntry[] Load()
    {
        using var stream = EmbeddedCatalog.OpenBrotliBase64("KillerTools.Engine.Data.port-protocol.br64");
        var groups = JsonSerializer.Deserialize<Group[]>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("The port and protocol catalog is invalid.");
        return groups.SelectMany(group => group.Ports.Select(port => new PortProtocolEntry(group.Category, port.Port.Clone(), port.Protocol, port.Service, port.Description, port.Dangerous, port.Notes))).ToArray();
    }
}
