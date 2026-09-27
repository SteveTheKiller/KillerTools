using KillerMCP.Runtime;

var root = Path.Combine(Path.GetTempPath(), "killermcp-discovery-" + Guid.NewGuid().ToString("N"));
try
{
    var local = Path.Combine(root, "Local");
    var machine = Path.Combine(root, "Machine");
    var expected = new Dictionary<string, string>
    {
        ["killerpdf"] = Path.Combine(local, "Programs", "KillerPDF", "KillerPDF.App.exe"),
        ["killerscan"] = Path.Combine(machine, "KillerScan", "KillerScan.exe"),
        ["killershell"] = Path.Combine(local, "Programs", "KillerShell", "KillerShell.exe"),
        ["killerbench"] = Path.Combine(machine, "KillerBench", "killerbench-cli.exe"),
        ["killernotes"] = Path.Combine(machine, "KillerNotes", "KillerNotes.exe"),
        ["killendar"] = Path.Combine(local, "Programs", "Killendar", "Killendar.exe"),
    };
    foreach (var path in expected.Values)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "fixture");
    }

    var environment = new Dictionary<string, string?> { ["LOCALAPPDATA"] = local, ["ProgramFiles"] = machine };
    foreach (var item in expected)
    {
        Equal(item.Value, AppDiscovery.Discover(item.Key, environment));
    }

    Equal(expected.Count, AppDiscovery.DiscoverAll(environment).Count);
    environment["KILLERPDF_CLI"] = expected["killerpdf"];
    Equal(expected["killerpdf"], AppDiscovery.Discover("killerpdf", environment));
    environment["KILLERPDF_CLI"] = Path.Combine(root, "missing.exe");
    Equal<string?>(null, AppDiscovery.Discover("killerpdf", environment));
    Throws<ArgumentException>(() => AppDiscovery.Discover("unknown", environment));
    Console.WriteLine("PASS native KillerMCP app discovery");
}
finally
{
    if (Directory.Exists(root))
    {
        Directory.Delete(root, true);
    }
}

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
