using System.Reflection;
using System.Text.Json;

namespace KillerTools.Engine.SystemTools;

public sealed record PowerShellCmdletSummary(string Cmdlet, string Module, string Category, string Description, bool RequiresAdmin);
public sealed record PowerShellCommand(string Command, bool RequiresAdmin);

public static class PowerShellBuilder
{
    private static readonly JsonElement[] Cmdlets = Load();

    public static IReadOnlyList<PowerShellCmdletSummary> Search(string query, int limit = 10)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        if (limit is < 1 or > 20) throw new ArgumentOutOfRangeException(nameof(limit));
        var normalized = query.Trim();
        return Cmdlets
            .Where(item => SearchText(item).Contains(normalized, StringComparison.OrdinalIgnoreCase))
            .Take(limit)
            .Select(item => new PowerShellCmdletSummary(
                item.GetProperty("cmdlet").GetString()!,
                item.GetProperty("module").GetString()!,
                item.GetProperty("category").GetString()!,
                item.GetProperty("description").GetString()!,
                item.GetProperty("requiresAdmin").GetBoolean()))
            .ToArray();
    }

    public static JsonElement Get(string cmdlet) => Find(cmdlet).Clone();

    public static PowerShellCommand Build(string cmdlet, JsonElement parameters)
    {
        var item = Find(cmdlet);
        var supplied = parameters.EnumerateObject().ToArray();
        if (supplied.Length > 20) throw new InvalidOperationException("Too many parameters");
        var known = item.GetProperty("parameters").EnumerateArray().ToArray();
        if (supplied.Any(value => !known.Any(parameter => parameter.GetProperty("name").GetString()!.Equals(value.Name, StringComparison.OrdinalIgnoreCase))))
            throw new InvalidOperationException("Unknown parameter");

        var parts = new List<string> { item.GetProperty("cmdlet").GetString()! };
        foreach (var parameter in known)
        {
            var name = parameter.GetProperty("name").GetString()!;
            var matches = supplied.Where(value => value.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length == 0) continue;
            var value = matches[0].Value;
            if ((value.ValueKind == JsonValueKind.String && value.GetString() == "") || (value.ValueKind == JsonValueKind.False)) continue;
            var type = parameter.GetProperty("type").GetString();
            if (type == "switch")
            {
                if (value.ValueKind != JsonValueKind.True) throw new InvalidOperationException($"-{name} must be a switch");
                parts.Add($"-{name}");
                continue;
            }
            if (value.ValueKind != JsonValueKind.String) throw new InvalidOperationException($"-{name} requires text");
            var text = value.GetString()!.Trim();
            if (text.Length == 0) continue;
            parts.Add(type switch
            {
                "int" when !System.Text.RegularExpressions.Regex.IsMatch(text, "^-?\\d+$") => throw new InvalidOperationException($"-{name} requires an integer"),
                "int" => $"-{name} {text}",
                "scriptblock" => $"-{name} {{ {text} }}",
                "string[]" => BuildArray(name, text),
                _ => $"-{name} {Quote(text)}",
            });
        }
        var command = string.Join(' ', parts);
        if (command.Length > 4096) throw new InvalidOperationException("Generated command is too long");
        return new PowerShellCommand(command, item.GetProperty("requiresAdmin").GetBoolean());
    }

    private static string BuildArray(string name, string value)
    {
        var items = value.Split(',').Select(item => item.Trim()).Where(item => item.Length > 0).ToArray();
        if (items.Length > 20) throw new InvalidOperationException($"-{name} has too many items");
        return $"-{name} {string.Join(',', items.Select(Quote))}";
    }

    private static string Quote(string value) => $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";
    private static string SearchText(JsonElement item) => $"{item.GetProperty("cmdlet").GetString()} {item.GetProperty("description").GetString()} {item.GetProperty("module").GetString()}";
    private static JsonElement Find(string cmdlet) => Cmdlets.FirstOrDefault(item => item.GetProperty("cmdlet").GetString()!.Equals(cmdlet, StringComparison.OrdinalIgnoreCase)).ValueKind == JsonValueKind.Undefined
        ? throw new KeyNotFoundException("Unknown cmdlet")
        : Cmdlets.First(item => item.GetProperty("cmdlet").GetString()!.Equals(cmdlet, StringComparison.OrdinalIgnoreCase));

    private static JsonElement[] Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("KillerTools.Engine.Data.powershell-cmdlets.json") ?? throw new InvalidOperationException("PowerShell catalog is missing.");
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.EnumerateArray().Select(item => item.Clone()).ToArray();
    }
}
