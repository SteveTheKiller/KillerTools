using System.Runtime.InteropServices.JavaScript;
using QRCoder;

public static partial class BrowserHarness
{
    private static void Main() { }

    [JSExport]
    public static int RunChecks() => Program.Main([]);

    [JSExport]
    public static string Fixture()
    {
        var image = Render.Code("https://killershot.net/qr", QRCodeGenerator.ECCLevel.M, scale: 4);
        return "{\"gray\":\"" + Convert.ToBase64String(image.Pixels) + "\",\"width\":" + image.Width + ",\"height\":" + image.Height + "}";
    }
}
