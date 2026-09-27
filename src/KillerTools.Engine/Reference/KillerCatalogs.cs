using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KillerTools.Engine.Reference;

public static partial class KillerCatalogs
{
    private const string ScriptBaseUrl = "https://raw.githubusercontent.com/SteveTheKiller/killer-scripts/main/";

    private static readonly IReadOnlyList<IReadOnlyDictionary<string, object>> Modules =
    [
        new Dictionary<string, object>
        {
            ["name"] = "KillerPivot",
            ["expansion"] = "Per-Identity Validated Org-Toggle",
            ["description"] = "Switch between Microsoft 365 tenants in PowerShell without firing commands at the wrong one. KillerPivot disables the auth broker to force a real sign-in prompt, then asks the session which tenant it actually landed in and verifies it before you touch anything. Save your tenants once, then pivot between them by name with identity verification built in.",
            ["install"] = "Install-Module KillerPivot -Scope CurrentUser",
            ["repo"] = "https://github.com/SteveTheKiller/killer-modules/tree/main/KillerPivot",
            ["functions"] = new object[]
            {
                new Dictionary<string, object> { ["cmd"] = "Connect-PivotTenant", ["alias"] = "pivot" },
                new Dictionary<string, object> { ["cmd"] = "Get-PivotContext", ["alias"] = "pvc" },
                new Dictionary<string, object> { ["cmd"] = "Disconnect-PivotTenant", ["alias"] = "pvx" },
                new Dictionary<string, object> { ["cmd"] = "Add-PivotTenant" },
                new Dictionary<string, object> { ["cmd"] = "Get-PivotTenant" },
                new Dictionary<string, object> { ["cmd"] = "Remove-PivotTenant" },
            },
            ["requirements"] = "PowerShell 5.1 & 7 · ExchangeOnlineManagement v3+ · Microsoft.Graph.Authentication (for -Graph)",
        },
        new Dictionary<string, object>
        {
            ["name"] = "KillerScripts",
            ["description"] = "Every tool in the killer-scripts repo, available as a command in any PowerShell session. Each script is pulled fresh from GitHub the moment you run it, so you are always on the current version, with a local cache as an offline fallback. All 18 tools get their own command and a short alias, so typing urt runs the Universal Rename Tool.",
            ["install"] = "Install-Module KillerScripts -Scope CurrentUser",
            ["repo"] = "https://github.com/SteveTheKiller/killer-modules/tree/main/KillerScripts",
            ["functions"] = new object[]
            {
                new Dictionary<string, object> { ["cmd"] = "Get-KillerScript" },
                new Dictionary<string, object> { ["cmd"] = "Invoke-KillerScript" },
                new Dictionary<string, object> { ["cmd"] = "Update-KillerScripts" },
            },
            ["requirements"] = "PowerShell 5.1 & 7 · Internet access to fetch the current script (cached copies run offline)",
        },
    ];

    public static IReadOnlyList<IReadOnlyDictionary<string, object>> ListModules(string? name = null) => string.IsNullOrWhiteSpace(name)
        ? Modules
        : Modules.Where(module => ((string)module["name"]).Contains(name, StringComparison.OrdinalIgnoreCase)).ToArray();

    public static async Task<IReadOnlyList<ScriptCatalogEntry>> ListScriptsAsync(string? query = null, HttpClient? client = null, CancellationToken cancellationToken = default)
    {
        using var ownedClient = client is null ? new HttpClient { Timeout = TimeSpan.FromSeconds(5) } : null;
        client ??= ownedClient!;
        using var response = await client.GetAsync(ScriptBaseUrl + "descriptions.json", HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.OK || response.Content.Headers.ContentLength > 32_768) throw new InvalidDataException("Script catalog unavailable");
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = new byte[4096];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > 32_768) throw new InvalidDataException("Script catalog too large");
            buffer.Write(chunk, 0, read);
        }

        using var document = JsonDocument.Parse(buffer.ToArray());
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Invalid script catalog");
        var entries = new List<ScriptCatalogEntry>();
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (!ScriptName().IsMatch(property.Name)) continue;
            if (property.Value.ValueKind != JsonValueKind.Object || !property.Value.TryGetProperty("name", out var nameValue) || !property.Value.TryGetProperty("description", out var descriptionValue)
                || nameValue.ValueKind != JsonValueKind.String || descriptionValue.ValueKind != JsonValueKind.String) throw new InvalidDataException("Invalid script entry");
            var name = nameValue.GetString()!;
            var description = descriptionValue.GetString()!;
            if (name.Length > 256 || description.Length > 512) throw new InvalidDataException("Invalid script entry");
            entries.Add(new(property.Name, name, description, ScriptBaseUrl + property.Name));
        }
        if (entries.Count > 100) throw new InvalidDataException("Script catalog too large");
        return entries.OrderBy(entry => entry.Filename, StringComparer.CurrentCulture)
            .Where(entry => string.IsNullOrWhiteSpace(query) || $"{entry.Filename} {entry.Name} {entry.Description}".Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
    }

    [GeneratedRegex("^[A-Za-z0-9-]{1,64}\\.ps1$")]
    private static partial Regex ScriptName();
}

public sealed record ScriptCatalogEntry(string Filename, string Name, string Description, string DownloadUrl);
