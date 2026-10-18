using QRCoder;

/// <summary>Test payloads. Text is generated, never derived from the reader under test.</summary>
internal static class Corpus
{
    /// <summary>Lowercase text (byte mode) about 90 percent as long as QRCoder will fit in the version and level.</summary>
    public static string Fill(int version, QRCodeGenerator.ECCLevel level)
    {
        int lo = 1, hi = 3000;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (Fits(Text(mid, version), level, version)) lo = mid; else hi = mid - 1;
        }
        return Text(Math.Max(1, lo * 9 / 10), version);
    }

    public static string Text(int length, int seed)
    {
        var random = new Random(seed * 7919 + length);
        const string letters = "abcdefghijklmnopqrstuvwxyz0123456789 .,-";
        var chars = new char[length];
        for (int i = 0; i < length; i++) chars[i] = letters[random.Next(letters.Length)];
        return new string(chars);
    }

    private static bool Fits(string text, QRCodeGenerator.ECCLevel level, int version)
    {
        try
        {
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(text, level, false, false, QRCodeGenerator.EciMode.Default, version);
            return true;
        }
        catch (Exception) { return false; }
    }
}
