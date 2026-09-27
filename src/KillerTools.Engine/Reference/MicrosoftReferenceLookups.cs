using System.Globalization;

namespace KillerTools.Engine.Reference;

public sealed record M365SkuEntry(string Category, string Guid, string StringId, string Name, string Description, string Tier);
public sealed record WindowsErrorEntry(string Category, string Hex, int Decimal, string Name, string Description, string Severity);
public sealed record WindowsEventEntry(string Category, string Log, int Id, string Name, string Description, string Severity);
public sealed record ExchangeNdrEntry(string Category, string Code, string Name, string Description, string Cause, string Fix, string Severity);

public static class MicrosoftReferenceLookups
{
    private sealed record SkuGroup(string Category, Sku[] Skus);
    private sealed record Sku(string Guid, string StringId, string Name, string Description, string Tier);
    private sealed record ErrorGroup(string Category, Error[] Errors);
    private sealed record Error(string Hex, int Decimal, string Name, string Description, string Severity);
    private sealed record EventGroup(string Category, string Log, Event[] Events);
    private sealed record Event(int Id, string Name, string Description, string Severity);
    private sealed record NdrGroup(string Category, Ndr[] Codes);
    private sealed record Ndr(string Code, string Name, string Description, string Cause, string Fix, string Severity);

    private static readonly Lazy<M365SkuEntry[]> Skus = new(() => EmbeddedCatalog.DeserializeBrotliBase64<SkuGroup[]>("KillerTools.Engine.Data.m365-skus.br64").SelectMany(group => group.Skus.Select(item => new M365SkuEntry(group.Category, item.Guid, item.StringId, item.Name, item.Description, item.Tier))).ToArray());
    private static readonly Lazy<WindowsErrorEntry[]> Errors = new(() => EmbeddedCatalog.DeserializeBrotliBase64<ErrorGroup[]>("KillerTools.Engine.Data.windows-errors.br64").SelectMany(group => group.Errors.Select(item => new WindowsErrorEntry(group.Category, item.Hex, item.Decimal, item.Name, item.Description, item.Severity))).ToArray());
    private static readonly Lazy<WindowsEventEntry[]> Events = new(() => EmbeddedCatalog.DeserializeBrotliBase64<EventGroup[]>("KillerTools.Engine.Data.windows-events.br64").SelectMany(group => group.Events.Select(item => new WindowsEventEntry(group.Category, group.Log, item.Id, item.Name, item.Description, item.Severity))).ToArray());
    private static readonly Lazy<ExchangeNdrEntry[]> Ndrs = new(() => EmbeddedCatalog.DeserializeBrotliBase64<NdrGroup[]>("KillerTools.Engine.Data.exchange-ndr.br64").SelectMany(group => group.Codes.Select(item => new ExchangeNdrEntry(group.Category, item.Code, item.Name, item.Description, item.Cause, item.Fix, item.Severity))).ToArray());

    public static IReadOnlyList<M365SkuEntry> SearchSkus(string query, int limit) => Skus.Value.Where(item => Match(query, item.Guid, item.StringId, item.Name, item.Description)).Take(limit).ToArray();
    public static IReadOnlyList<WindowsErrorEntry> SearchErrors(string query, int limit) => Errors.Value.Where(item => Match(query, item.Decimal, item.Hex, item.Name, item.Description)).Take(limit).ToArray();
    public static IReadOnlyList<WindowsEventEntry> SearchEvents(string query, int limit) => Events.Value.Where(item => Match(query, item.Id, item.Name, item.Description)).Take(limit).ToArray();
    public static IReadOnlyList<ExchangeNdrEntry> SearchNdrs(string query, int limit) => Ndrs.Value.Where(item => Match(query, item.Code, item.Name, item.Description, item.Cause, item.Fix)).Take(limit).ToArray();

    public static (int Skus, int Errors, int Events, int Ndrs) Counts => (Skus.Value.Length, Errors.Value.Length, Events.Value.Length, Ndrs.Value.Length);

    private static bool Match(string query, params object[] values) => values.Any(value => Convert.ToString(value, CultureInfo.InvariantCulture)!.Contains(query, StringComparison.OrdinalIgnoreCase));
}
