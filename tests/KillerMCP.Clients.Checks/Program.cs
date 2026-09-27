using System.Text.Json.Nodes;
using KillerMCP.Clients;

var root = Path.Combine(Path.GetTempPath(), "killermcp-clients-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var executable = Path.Combine(root, OperatingSystem.IsWindows() ? "KillerMCP.exe" : "KillerMCP");
    var configuration = Path.Combine(root, "client", "mcp.json");
    Directory.CreateDirectory(Path.GetDirectoryName(configuration)!);
    File.WriteAllText(configuration, """
        {
          "preservedSetting": "keep",
          "mcpServers": {
            "existing": {
              "command": "existing-command",
              "args": ["existing-argument"]
            }
          }
        }
        """);

    Equal("KillerMCP was added to Cursor.", ClientRegistration.RegisterJsonClient(configuration, "Cursor", executable));
    var saved = Read(configuration);
    Equal("keep", saved["preservedSetting"]!.GetValue<string>());
    Equal("existing-command", saved["mcpServers"]!["existing"]!["command"]!.GetValue<string>());
    Equal(executable, saved["mcpServers"]!["killermcp"]!["command"]!.GetValue<string>());
    Equal(0, saved["mcpServers"]!["killermcp"]!["args"]!.AsArray().Count);

    Equal("Cursor already has the correct KillerMCP connection.", ClientRegistration.RegisterJsonClient(configuration, "Cursor", executable));
    saved["mcpServers"]!["killermcp"]!["command"] = Path.Combine(root, "different-host");
    File.WriteAllText(configuration, saved.ToJsonString());
    Throws<InvalidOperationException>(() => ClientRegistration.RegisterJsonClient(configuration, "Cursor", executable));
    Equal(Path.Combine(root, "different-host"), Read(configuration)["mcpServers"]!["killermcp"]!["command"]!.GetValue<string>());

    var claude = Path.Combine(root, "claude.json");
    ClientRegistration.RegisterJsonClient(claude, "Claude Code", executable, includeStdioType: true);
    Equal("stdio", Read(claude)["mcpServers"]!["killermcp"]!["type"]!.GetValue<string>());
    Equal(true, ClientRegistration.RemoveJsonClient(claude, "Claude Code", executable, includeStdioType: true));
    Equal(false, Read(claude)["mcpServers"]!.AsObject().ContainsKey("killermcp"));
    Equal(false, ClientRegistration.RemoveJsonClient(Path.Combine(root, "missing.json"), "Cursor", executable));

    var malformed = Path.Combine(root, "malformed.json");
    File.WriteAllText(malformed, "{");
    Throws<InvalidDataException>(() => ClientRegistration.RegisterJsonClient(malformed, "Cursor", executable));
    Console.WriteLine("PASS native KillerMCP client registration");
}
finally
{
    Directory.Delete(root, true);
}

static JsonObject Read(string path) => JsonNode.Parse(File.ReadAllText(path))!.AsObject();

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected {expected}, received {actual}.");
    }
}

static void Throws<T>(Action action) where T : Exception
{
    try
    {
        action();
    }
    catch (T)
    {
        return;
    }

    throw new InvalidOperationException($"Expected {typeof(T).Name}.");
}
