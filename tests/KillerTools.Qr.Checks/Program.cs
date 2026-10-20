using KillerTools.Qr;
using QRCoder;

internal static class Program
{
    private static int _checks;

    public static int Main(string[] args)
    {
        try
        {
            if (args.Length is 2 or 3 && args[0] == "--write-fixture")
            {
                using var generator = new QRCodeGenerator();
                using var data = generator.CreateQrCode(args.Length == 3 ? args[2] : "https://killershot.net/qr", QRCodeGenerator.ECCLevel.M);
                using var png = new PngByteQRCode(data);
                File.WriteAllBytes(args[1], png.GetGraphic(5));
                return 0;
            }
            ValidatesPixelBuffer();
            ReadsAPlainCode();
            LinkChecks.Run();
            if (args.Contains("--links-only"))
            {
                Console.WriteLine($"PASS {_checks} QR and link checks.");
                return 0;
            }
            ReadsTurnedAndTilted();
            ChecksVersionInformation();
            ReadsUnevenBackground();
            ReadsTwoCodes();
            RejectsBlankAndNoise();
            ReadsKanji();
            ReadsEveryVersionAndLevel();
            ReadsEveryTextMode();
            CorrectsDamage();
            ReadsLightOnDark();
            Console.WriteLine($"PASS {_checks} QR reader checks.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine("FAIL " + error.Message); return 1; }
    }

    private static void ValidatesPixelBuffer()
    {
        Reject(() => QrReader.Read(new byte[2], 1, 1), typeof(ArgumentException), "gray", "oversized buffer");
        Reject(() => QrReader.Read([], 1, 1), typeof(ArgumentException), "gray", "short buffer");
        Reject(() => QrReader.Read(null!, 1, 1), typeof(ArgumentNullException), "gray", "null buffer");
        foreach (int dimension in new[] { 0, -1, int.MinValue })
        {
            Reject(() => QrReader.Read([], dimension, 1), typeof(ArgumentOutOfRangeException), "width", "nonpositive width");
            Reject(() => QrReader.Read([], 1, dimension), typeof(ArgumentOutOfRangeException), "height", "nonpositive height");
        }
        Reject(() => QrReader.Read([], 65536, 65536), typeof(ArgumentException), "gray", "pixel count wraps to zero in 32 bits");
        Reject(() => QrReader.Read([255], int.MaxValue, int.MaxValue), typeof(ArgumentException), "gray", "pixel count wraps to one in 32 bits");
        Equal(0, QrReader.Read([255], 1, 1).Count, "exact one-pixel buffer has no code");

        static void Reject(Action read, Type expected, string parameter, string scenario)
        {
            try { read(); }
            catch (Exception error)
            {
                Equal(expected, error.GetType(), scenario + " reports an argument error");
                Equal(parameter, ((ArgumentException)error).ParamName, scenario + " identifies the argument");
                return;
            }
            throw new InvalidOperationException(scenario + " was accepted");
        }
    }

    /// <summary>A small upright code at 4 px a module reads back as the text it holds.</summary>
    private static void ReadsAPlainCode()
    {
        var image = Render.Code("Hello, KillerShot", QRCodeGenerator.ECCLevel.M, scale: 4);
        var found = QrReader.Read(image.Pixels, image.Width, image.Height);
        Equal(1, found.Count, "one code is found");
        Equal("Hello, KillerShot", found[0].Text, "its text reads back");
    }

    /// <summary>Versions 1 to 40 at all four levels, each filled close to capacity so every block
    /// group of the version table is exercised.</summary>
    private static void ReadsEveryVersionAndLevel()
    {
        var failures = new List<string>();
        foreach (var level in new[] { QRCodeGenerator.ECCLevel.L, QRCodeGenerator.ECCLevel.M, QRCodeGenerator.ECCLevel.Q, QRCodeGenerator.ECCLevel.H })
            for (int version = 1; version <= 40; version++)
            {
                string text = Corpus.Fill(version, level);
                var found = QrReader.Read(Render.Code(text, level, scale: 2, version: version) is var g ? g.Pixels : [], g.Width, g.Height);
                _checks++;
                if (found.Count != 1 || found[0].Text != text || found[0].Version != version || found[0].ErrorCorrection.ToString() != level.ToString())
                    failures.Add($"v{version}-{level}: {(found.Count == 0 ? "not found" : found[0].Text == text ? $"read as v{found[0].Version}-{found[0].ErrorCorrection}" : "wrong text")}");
            }
        if (failures.Count > 0) throw new InvalidOperationException($"{failures.Count} of 160 version and level codes failed: " + string.Join(", ", failures.Take(20)));
    }

    /// <summary>Numeric, alphanumeric and byte segments, and UTF-8 with and without an ECI marker.</summary>
    private static void ReadsEveryTextMode()
    {
        foreach (var (text, eci, label) in new (string, QRCodeGenerator.EciMode, string)[]
        {
            ("0123456789012345678901234567890", QRCodeGenerator.EciMode.Default, "numeric"),
            ("12", QRCodeGenerator.EciMode.Default, "numeric, two digits"),
            ("HTTPS://KILLERTOOLS.NET/QR $%*+-./:", QRCodeGenerator.EciMode.Default, "alphanumeric"),
            ("https://killertools.net/qr?code=Ab12", QRCodeGenerator.EciMode.Default, "byte"),
            ("naïve café, Zürich", QRCodeGenerator.EciMode.Default, "Latin-1 text"),
            ("日本語のテキスト and emoji 😀", QRCodeGenerator.EciMode.Default, "UTF-8 without an ECI marker"),
            ("Привет, мир", QRCodeGenerator.EciMode.Utf8, "UTF-8 with an ECI marker"),
            ("WIFI:S:Shop Floor;T:WPA;P:p@ss;w0rd;;", QRCodeGenerator.EciMode.Default, "a Wi-Fi payload"),
        })
        {
            var image = Render.Upright(Render.Modules(text, QRCodeGenerator.ECCLevel.M, eci: eci), 3);
            var found = QrReader.Read(image.Pixels, image.Width, image.Height);
            Equal(1, found.Count, label + " is found");
            Equal(text, found[0].Text, label + " reads back");
        }
    }

    /// <summary>A smudge over part of the data still reads, through the error correction codewords, and a
    /// code damaged past repair reads as nothing rather than as wrong text.</summary>
    private static void CorrectsDamage()
    {
        const string text = "Ticket 4471: printer offline on the third floor";
        var modules = Render.Modules(text, QRCodeGenerator.ECCLevel.H);
        Smudge(modules, 14, 14, 5);
        var found = QrReader.Read(Render.Upright(modules, 3) is var g ? g.Pixels : [], g.Width, g.Height);
        Equal(1, found.Count, "a smudged code is still found");
        Equal(text, found[0].Text, "a smudged code reads back through error correction");

        var ruined = Render.Modules(text, QRCodeGenerator.ECCLevel.L);
        Smudge(ruined, 12, 12, 14);
        found = QrReader.Read(Render.Upright(ruined, 3) is var r ? r.Pixels : [], r.Width, r.Height);
        Equal(0, found.Count, "a code damaged past repair reads as nothing");
    }

    /// <summary>Inverts a square of modules (coordinates include QRCoder's 4-module quiet zone).</summary>
    private static void Smudge(bool[,] modules, int left, int top, int size)
    {
        for (int y = top; y < top + size; y++)
            for (int x = left; x < left + size; x++) modules[y, x] = !modules[y, x];
    }

    /// <summary>A code drawn light on a dark background, as dark themes and terminals show them.</summary>
    private static void ReadsLightOnDark()
    {
        const string text = "https://killershot.net";
        var image = Render.Upright(Render.Modules(text, QRCodeGenerator.ECCLevel.M), 3, inverted: true);
        var found = QrReader.Read(image.Pixels, image.Width, image.Height);
        Equal(1, found.Count, "a light-on-dark code is found");
        Equal(text, found[0].Text, "a light-on-dark code reads back");
    }

    /// <summary>Codes turned a quarter turn at a time and by a small angle, at fractional module sizes with
    /// anti-aliased edges. Steeper angles and perspective are the next slice (alignment patterns).</summary>
    private static void ReadsTurnedAndTilted()
    {
        foreach (var (degrees, scale, tilt, version) in new (double, double, double, int)[]
        {
            (90, 3, 0, 2), (180, 3, 0, 2), (270, 3, 0, 2), (15, 3.4, 0, 3),
            (33, 2.6, 0, 5),
            (-47, 4.2, 0, 4),
            (128, 3, 0, 7),
            (0, 3.5, 0.25, 4),
            (20, 3.5, 0.3, 6),
            (0, 2.5, 0, 10),
        })
        {
            string text = Corpus.Fill(version, QRCodeGenerator.ECCLevel.M);
            var image = Render.Turned(Render.Modules(text, QRCodeGenerator.ECCLevel.M, version), scale, degrees, tilt);
            var found = QrReader.Read(image.Pixels, image.Width, image.Height);
            string label = $"v{version} turned {degrees} degrees at {scale} px a module, tilt {tilt}";
            Equal(1, found.Count, label + " is found");
            Equal(text, found[0].Text, label + " reads back");
        }
    }

    private static void ReadsKanji()
    {
        var image = Render.Upright(KanjiFixture.Modules(), 4);
        var found = QrReader.Read(image.Pixels, image.Width, image.Height);
        Equal(1, found.Count, "a hand-built kanji segment is found");
        Equal("漢字", found[0].Text, "13-bit kanji characters read back");
    }

    private static void RejectsBlankAndNoise()
    {
        Equal(0, QrReader.Read(Enumerable.Repeat((byte)255, 256 * 192).ToArray(), 256, 192).Count, "blank paper has no codes");
        var random = new Random(4471);
        var noise = new byte[256 * 192];
        random.NextBytes(noise);
        Equal(0, QrReader.Read(noise, 256, 192).Count, "deterministic noise has no codes");
    }

    private static void ReadsTwoCodes()
    {
        var first = Render.Code("First capture", QRCodeGenerator.ECCLevel.M, 4, 2);
        var second = Render.Code("https://killershot.net/second", QRCodeGenerator.ECCLevel.H, 3, 4);
        int width = first.Width + second.Width + 30, height = Math.Max(first.Height, second.Height) + 20;
        var pixels = Enumerable.Repeat((byte)255, width * height).ToArray();
        for (int y = 0; y < first.Height; y++) Array.Copy(first.Pixels, y * first.Width, pixels, (y + 10) * width + 5, first.Width);
        for (int y = 0; y < second.Height; y++) Array.Copy(second.Pixels, y * second.Width, pixels, (y + 10) * width + first.Width + 25, second.Width);
        var found = QrReader.Read(pixels, width, height);
        Equal(2, found.Count, "two separate codes are returned once each");
        Equal("First capture|https://killershot.net/second", string.Join('|', found.Select(r => r.Text).Order()), "both payloads read back");
    }

    private static void ReadsUnevenBackground()
    {
        const string text = "https://killershot.net/uneven-background";
        var image = Render.Code(text, QRCodeGenerator.ECCLevel.M, 5, 4);
        for (int y = 0; y < image.Height; y++)
            for (int x = 0; x < image.Width; x++)
            {
                int shade = 30 + 150 * x / image.Width;
                image.Pixels[y * image.Width + x] = (byte)(shade + (image.Pixels[y * image.Width + x] == 0 ? 0 : 55));
            }
        var found = QrReader.Read(image.Pixels, image.Width, image.Height);
        Equal(1, found.Count, "a code across a strong lighting gradient is found");
        Equal(text, found[0].Text, "a code across a strong lighting gradient reads back");
    }

    private static void ChecksVersionInformation()
    {
        var modules = Render.Modules(Corpus.Fill(7, QRCodeGenerator.ECCLevel.M), QRCodeGenerator.ECCLevel.M, 7);
        int dimension = modules.GetLength(0) - 8;
        // A valid version-8 BCH word in both places contradicts this version-7 symbol.
        const int versionEight = 0x085bc;
        for (int i = 0; i < 18; i++)
        {
            bool dark = (versionEight & (1 << i)) != 0;
            modules[4 + i / 3, 4 + dimension - 11 + i % 3] = dark;
            modules[4 + dimension - 11 + i % 3, 4 + i / 3] = dark;
        }
        var image = Render.Upright(modules, 3);
        Equal(0, QrReader.Read(image.Pixels, image.Width, image.Height).Count, "contradictory version information is rejected");
    }

    internal static void Equal<T>(T expected, T actual, string scenario)
    {
        _checks++;
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{scenario}: expected <{expected}> but got <{actual}>");
    }
}
