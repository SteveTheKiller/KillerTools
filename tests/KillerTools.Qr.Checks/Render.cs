using QRCoder;

/// <summary>An 8-bit grayscale test image, 0 black to 255 white.</summary>
internal sealed record Gray(byte[] Pixels, int Width, int Height);

/// <summary>Draws QRCoder's module matrix as a picture, the way a screenshot would hold it.</summary>
internal static class Render
{
    public static bool[,] Modules(string text, QRCodeGenerator.ECCLevel level, int version = -1, QRCodeGenerator.EciMode eci = QRCodeGenerator.EciMode.Default)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, level, false, false, eci, version);
        int n = data.ModuleMatrix.Count;
        var m = new bool[n, n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++) m[y, x] = data.ModuleMatrix[y][x];
        return m;
    }

    public static Gray Code(string text, QRCodeGenerator.ECCLevel level, int scale, int version = -1) => Upright(Modules(text, level, version), scale);

    /// <summary>Whole-pixel modules, black on white, with QRCoder's quiet zone.</summary>
    public static Gray Upright(bool[,] modules, int scale, bool inverted = false)
    {
        int n = modules.GetLength(0), size = n * scale;
        var px = new byte[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                px[y * size + x] = (byte)(modules[y / scale, x / scale] ^ inverted ? 0 : 255);
        return new Gray(px, size, size);
    }

    /// <summary>Draws the code turned by an angle, at a fractional module size, with anti-aliased edges
    /// (4x4 samples a pixel), on a white canvas. A nonzero tilt narrows the far edge for perspective.</summary>
    public static Gray Turned(bool[,] modules, double scale, double degrees, double tilt = 0, int margin = 20)
    {
        int n = modules.GetLength(0);
        double side = n * scale, angle = degrees * Math.PI / 180;
        double cos = Math.Cos(angle), sin = Math.Sin(angle);
        int size = (int)Math.Ceiling(side * (Math.Abs(cos) + Math.Abs(sin))) + 2 * margin;
        double c = size / 2.0;
        var px = new byte[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int dark = 0;
                for (int sy = 0; sy < 4; sy++)
                    for (int sx = 0; sx < 4; sx++)
                    {
                        double dx = x + (sx + 0.5) / 4 - c, dy = y + (sy + 0.5) / 4 - c;
                        // Undo the turn, then the tilt: rows further down are drawn narrower.
                        double u = cos * dx + sin * dy, v = -sin * dx + cos * dy;
                        double vNorm = v / side + 0.5;
                        double widen = 1 + tilt * (vNorm - 0.5);
                        u /= widen;
                        int mx = (int)Math.Floor(u / scale + n / 2.0), my = (int)Math.Floor(v / scale + n / 2.0);
                        if (mx >= 0 && my >= 0 && mx < n && my < n && modules[my, mx]) dark++;
                    }
                px[y * size + x] = (byte)(255 - dark * 255 / 16);
            }
        return new Gray(px, size, size);
    }
}
