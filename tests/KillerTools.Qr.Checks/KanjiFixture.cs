using QRCoder;

/// <summary>A hand-built version-1 L, mask-0 symbol for the kanji segment 漢字.</summary>
internal static class KanjiFixture
{
    public static bool[,] Modules()
    {
        // Mode 1000, count 00000010, 13-bit values 1855 (8ABF) and 2586 (8E9A), then terminator.
        byte[] data = [128, 35, 159, 168, 104, 0, 236, 17, 236, 17, 236, 17, 236, 17, 236, 17, 236, 17, 236];
        byte Multiply(int a, int b)
        {
            int value = 0;
            while (b != 0)
            {
                if ((b & 1) != 0) value ^= a;
                a <<= 1;
                if ((a & 256) != 0) a ^= 0x11d;
                b >>= 1;
            }
            return (byte)value;
        }
        // Polynomial long division, independent of the reader's error-correction implementation.
        byte[] generator = [1];
        int root = 1;
        for (int i = 0; i < 7; i++)
        {
            var next = new byte[generator.Length + 1];
            for (int j = 0; j < generator.Length; j++)
            {
                next[j] ^= generator[j];
                next[j + 1] ^= Multiply(generator[j], root);
            }
            generator = next;
            root = Multiply(root, 2);
        }
        var division = new byte[26];
        data.CopyTo(division, 0);
        for (int i = 0; i < data.Length; i++)
        {
            byte factor = division[i];
            for (int j = 0; j < generator.Length; j++) division[i + j] ^= Multiply(factor, generator[j]);
        }
        var words = data.Concat(division.Skip(19)).ToArray();
        // QRCoder supplies only the fixed finder/timing geometry, never this payload or its encoding.
        var modules = Render.Modules("", QRCodeGenerator.ECCLevel.L, 1);
        bool Reserved(int x, int y) => x == 6 || y == 6 || (x < 9 && y < 9)
            || (x >= 13 && y < 9) || (x < 9 && y >= 13);
        int bit = 0;
        for (int right = 20; right > 0; right -= 2)
        {
            if (right == 6) right = 5;
            for (int step = 0; step < 21; step++)
            {
                int y = ((right + 1) & 2) == 0 ? 20 - step : step;
                for (int j = 0; j < 2; j++)
                {
                    int x = right - j;
                    if (Reserved(x, y)) continue;
                    bool value = (words[bit / 8] & (128 >> (bit % 8))) != 0;
                    modules[y + 4, x + 4] = value ^ ((x + y) % 2 == 0);
                    bit++;
                }
            }
        }
        const int format = 0x77c4; // Level L, mask 0, including BCH and format mask.
        for (int i = 0; i < 15; i++)
        {
            (int x, int y) a = i < 6 ? (8, i) : i == 6 ? (8, 7) : i == 7 ? (8, 8) : i == 8 ? (7, 8) : (14 - i, 8);
            (int x, int y) b = i < 8 ? (20 - i, 8) : (8, 6 + i);
            modules[a.y + 4, a.x + 4] = modules[b.y + 4, b.x + 4] = (format & (1 << i)) != 0;
        }
        return modules;
    }
}
