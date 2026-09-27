using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using UAParser;

namespace KillerTools.Engine.Web;

public sealed record UserAgentDetails(
    [property: JsonPropertyName("browser")] IReadOnlyDictionary<string, string> Browser,
    [property: JsonPropertyName("engine")] IReadOnlyDictionary<string, string> Engine,
    [property: JsonPropertyName("os")] IReadOnlyDictionary<string, string> Os,
    [property: JsonPropertyName("device")] IReadOnlyDictionary<string, string> Device,
    [property: JsonPropertyName("cpu")] IReadOnlyDictionary<string, string> Cpu);

public static partial class UserAgentParser
{
    private static readonly Parser ParserInstance = Parser.GetDefault();

    public static UserAgentDetails Parse(string userAgent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userAgent);
        var parsed = ParserInstance.Parse(userAgent);
        var browser = Browser(userAgent, parsed.Browser.Family, parsed.Browser.Version);
        var engine = Engine(userAgent, browser);
        var os = Os(userAgent, parsed.OS.Family, Version(parsed.OS.Major, parsed.OS.Minor, parsed.OS.Patch, parsed.OS.PatchMinor));
        var device = Device(userAgent, parsed.Device.Brand, parsed.Device.Model, parsed.Device.Family);
        var cpu = Cpu(userAgent);
        return new UserAgentDetails(browser, engine, os, device, cpu);
    }

    private static Dictionary<string, string> Browser(string userAgent, string family, string version)
    {
        if (family is "Other" or "curl") return [];
        var result = new Dictionary<string, string> { ["name"] = family };
        var exact = BrowserVersionRegex(family).Match(userAgent);
        var value = exact.Success ? exact.Groups[1].Value : version;
        if (!string.IsNullOrEmpty(value))
        {
            result["version"] = value;
            result["major"] = value.Split('.')[0];
        }
        return result;
    }

    private static Dictionary<string, string> Engine(string userAgent, IReadOnlyDictionary<string, string> browser)
    {
        var name = browser.GetValueOrDefault("name");
        if (name is "Chrome" or "Chromium" or "Edge" or "Opera") return Named("Blink", browser.GetValueOrDefault("version"));
        if (name == "Firefox") return Named("Gecko", Match(userAgent, @"rv:([\d.]+)"));
        if (userAgent.Contains("AppleWebKit/", StringComparison.Ordinal)) return Named("WebKit", Match(userAgent, @"AppleWebKit/([\d.]+)"));
        if (userAgent.Contains("Trident/", StringComparison.Ordinal)) return Named("Trident", Match(userAgent, @"Trident/([\d.]+)"));
        return [];
    }

    private static Dictionary<string, string> Os(string userAgent, string family, string version)
    {
        if (family == "Other") return [];
        var result = new Dictionary<string, string> { ["name"] = family };
        if (family == "Linux") version = Match(userAgent, @"Linux\s+([^;)]+)");
        if (!string.IsNullOrEmpty(version)) result["version"] = version;
        return result;
    }

    private static Dictionary<string, string> Device(string userAgent, string brand, string model, string family)
    {
        if (family == "Other" && string.IsNullOrEmpty(brand) && string.IsNullOrEmpty(model)) return [];
        var result = new Dictionary<string, string>();
        if (userAgent.Contains("iPad", StringComparison.OrdinalIgnoreCase) || userAgent.Contains("Tablet", StringComparison.OrdinalIgnoreCase)) result["type"] = "tablet";
        else if (userAgent.Contains("Mobile", StringComparison.OrdinalIgnoreCase) || userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase)) result["type"] = "mobile";
        if (!string.IsNullOrEmpty(model)) result["model"] = model;
        else if (family != "Other") result["model"] = family;
        if (!string.IsNullOrEmpty(brand)) result["vendor"] = brand;
        return result;
    }

    private static Dictionary<string, string> Cpu(string userAgent)
    {
        var architecture = userAgent switch
        {
            _ when Regex.IsMatch(userAgent, @"(?:aarch64|arm64)", RegexOptions.IgnoreCase) => "arm64",
            _ when Regex.IsMatch(userAgent, @"(?:x86_64|x64|Win64|WOW64|amd64)", RegexOptions.IgnoreCase) => "amd64",
            _ when Regex.IsMatch(userAgent, @"(?:i[3-6]86|x86)", RegexOptions.IgnoreCase) => "ia32",
            _ when Regex.IsMatch(userAgent, @"(?:armv?7|arm)", RegexOptions.IgnoreCase) => "arm",
            _ => "",
        };
        return architecture.Length == 0 ? [] : new Dictionary<string, string> { ["architecture"] = architecture };
    }

    private static Dictionary<string, string> Named(string name, string? version)
    {
        var result = new Dictionary<string, string> { ["name"] = name };
        if (!string.IsNullOrEmpty(version)) result["version"] = version;
        return result;
    }

    private static Regex BrowserVersionRegex(string family) => family switch
    {
        "Chrome" => ChromeRegex(),
        "Firefox" => FirefoxRegex(),
        "Mobile Safari" or "Safari" => SafariRegex(),
        "Edge" => EdgeRegex(),
        "Opera" => OperaRegex(),
        _ => NeverRegex(),
    };
    private static string Match(string value, string pattern)
    {
        var match = Regex.Match(value, pattern, RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Replace('_', '.') : "";
    }
    private static string Version(params string?[] values) => string.Join('.', values.TakeWhile(value => !string.IsNullOrEmpty(value)));

    [GeneratedRegex(@"(?:Chrome|CriOS)/([\d.]+)", RegexOptions.IgnoreCase)] private static partial Regex ChromeRegex();
    [GeneratedRegex(@"(?:Firefox|FxiOS)/([\d.]+)", RegexOptions.IgnoreCase)] private static partial Regex FirefoxRegex();
    [GeneratedRegex(@"Version/([\d.]+)", RegexOptions.IgnoreCase)] private static partial Regex SafariRegex();
    [GeneratedRegex(@"(?:Edg|Edge|EdgiOS|EdgA)/([\d.]+)", RegexOptions.IgnoreCase)] private static partial Regex EdgeRegex();
    [GeneratedRegex(@"(?:OPR|Opera)/([\d.]+)", RegexOptions.IgnoreCase)] private static partial Regex OperaRegex();
    [GeneratedRegex("(?!)")] private static partial Regex NeverRegex();
}
