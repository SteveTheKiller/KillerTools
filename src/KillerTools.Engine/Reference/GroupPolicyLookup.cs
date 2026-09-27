using System.Globalization;

namespace KillerTools.Engine.Reference;

public sealed record GroupPolicyEntry(string Category, string Name, string Path, string Registry, string DefaultValue, string RecommendedValue, string Description, string Severity);

public static class GroupPolicyLookup
{
    private sealed record Group(string Category, Policy[] Policies);
    private sealed record Policy(string Name, string Path, string Registry, string DefaultValue, string RecommendedValue, string Description, string Severity);
    private static readonly Lazy<GroupPolicyEntry[]> Entries = new(() => EmbeddedCatalog.DeserializeBrotliBase64<Group[]>("KillerTools.Engine.Data.group-policy.br64")
        .SelectMany(group => group.Policies.Select(item => new GroupPolicyEntry(group.Category, item.Name, item.Path, item.Registry, item.DefaultValue, item.RecommendedValue, item.Description, item.Severity))).ToArray());

    public static int Count => Entries.Value.Length;

    public static IReadOnlyList<GroupPolicyEntry> Search(string query, int limit) =>
        Entries.Value.Where(item => Match(query, item.Name, item.Path, item.Registry, item.Description)).Take(limit).ToArray();

    private static bool Match(string query, params object[] values) =>
        values.Any(value => Convert.ToString(value, CultureInfo.InvariantCulture)!.Contains(query, StringComparison.OrdinalIgnoreCase));
}
