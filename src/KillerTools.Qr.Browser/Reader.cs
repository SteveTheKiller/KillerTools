using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.Json.Serialization;

[assembly: SupportedOSPlatform("browser")]

namespace KillerTools.Qr.Browser;

public static partial class Reader
{
    [JSExport]
    public static string Read(string grayscaleBase64, int width, int height)
    {
        if (width <= 0 || height <= 0 || (long)width * height > 16_000_000 || grayscaleBase64.Length > 21_333_336)
            throw new ArgumentException("The image must contain at most 16 million pixels.");
        byte[] gray = Convert.FromBase64String(grayscaleBase64);
        if (gray.Length != width * height) throw new ArgumentException("The grayscale image size is invalid.");
        var codes = QrReader.Read(gray, width, height).Select(code =>
        {
            var link = QrLink.Check(code.Text);
            return new BrowserCode(code.Text, code.Version, code.ErrorCorrection.ToString(), code.Corners.ToArray(),
                new BrowserLink(link.Disposition.ToString(), link.Url?.AbsoluteUri, link.Warnings.ToArray()), QrPayload.WifiPassword(code.Text));
        }).ToArray();
        return JsonSerializer.Serialize(codes, BrowserJson.Default.BrowserCodeArray);
    }
}

public sealed record BrowserLink(string Disposition, string? Url, string[] Warnings);
public sealed record BrowserCode(string Text, int Version, string ErrorCorrection, QrPoint[] Corners, BrowserLink Link, string? WifiPassword);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(BrowserCode[]))]
internal partial class BrowserJson : JsonSerializerContext;
