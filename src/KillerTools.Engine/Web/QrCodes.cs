using System.Text;
using System.Text.RegularExpressions;
using QRCoder;

namespace KillerTools.Engine.Web;

public sealed record WifiQrOptions(
    string Ssid,
    string Password,
    string Encryption,
    string? EapMethod,
    bool IsHiddenSsid,
    bool EapAnonymous,
    string EapIdentity,
    string? EapPhase2Method);

public static partial class QrCodes
{
    public static string GenerateSvg(string value, string foreground = "#000000", string background = "#ffffff", string errorCorrectionLevel = "medium")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (!ColorRegex().IsMatch(foreground) || !ColorRegex().IsMatch(background)) throw new ArgumentException("Invalid QR color.");
        var level = errorCorrectionLevel switch
        {
            "low" => QRCodeGenerator.ECCLevel.L,
            "medium" => QRCodeGenerator.ECCLevel.M,
            "quartile" => QRCodeGenerator.ECCLevel.Q,
            "high" => QRCodeGenerator.ECCLevel.H,
            _ => throw new ArgumentException("Invalid QR error correction level."),
        };
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(value, level);
        const int sourceQuietZone = 4;
        const int requestedQuietZone = 2;
        var sourceSize = data.ModuleMatrix.Count;
        var modules = sourceSize - sourceQuietZone * 2;
        var size = modules + requestedQuietZone * 2;
        var path = new StringBuilder();
        for (var row = sourceQuietZone; row < sourceSize - sourceQuietZone; row++)
        {
            for (var column = sourceQuietZone; column < sourceSize - sourceQuietZone; column++)
            {
                if (!data.ModuleMatrix[row][column]) continue;
                var x = column - sourceQuietZone + requestedQuietZone;
                var y = row - sourceQuietZone + requestedQuietZone;
                path.Append('M').Append(x).Append(' ').Append(y).Append("h1v1h-1z");
            }
        }
        var svg = $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"256\" height=\"256\" viewBox=\"0 0 {size} {size}\" shape-rendering=\"crispEdges\"><rect width=\"100%\" height=\"100%\" fill=\"{background}\"/><path d=\"{path}\" fill=\"{foreground}\"/></svg>";
        if (svg.Length > 65_536) throw new InvalidOperationException("QR output is too large");
        return svg;
    }

    public static string BuildWifiText(WifiQrOptions options)
    {
        if (string.IsNullOrEmpty(options.Ssid)) return "";
        var hidden = options.IsHiddenSsid ? "H:true;" : "";
        if (options.Encryption == "nopass") return $"WIFI:S:{Escape(options.Ssid)};;{hidden}";
        if (options.Encryption != "WPA2-EAP" && !string.IsNullOrEmpty(options.Password))
            return $"WIFI:S:{Escape(options.Ssid)};T:{options.Encryption};P:{Escape(options.Password)};{hidden}";
        if (options.Encryption == "WPA2-EAP" && !string.IsNullOrEmpty(options.Password) && !string.IsNullOrEmpty(options.EapMethod))
        {
            if (string.IsNullOrEmpty(options.EapIdentity) && !options.EapAnonymous) return "";
            if (options.EapMethod == "PEAP" && string.IsNullOrEmpty(options.EapPhase2Method)) return "";
            var identity = options.EapAnonymous ? "A:anon" : $"I:{Escape(options.EapIdentity)}";
            var phase2 = !string.IsNullOrEmpty(options.EapPhase2Method) && options.EapPhase2Method != "None" ? $"PH2:{options.EapPhase2Method};" : "";
            return $"WIFI:S:{Escape(options.Ssid)};T:WPA2-EAP;P:{Escape(options.Password)};E:{options.EapMethod};{phase2}{identity};{hidden}";
        }
        return "";
    }

    private static string Escape(string value) => WifiEscapeRegex().Replace(value, "\\$1");
    [GeneratedRegex("^#[0-9a-f]{6}$", RegexOptions.IgnoreCase)] private static partial Regex ColorRegex();
    [GeneratedRegex("([\\\\;,:\"])")] private static partial Regex WifiEscapeRegex();
}
