using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace KillerTools.Engine.Security;

public static class CryptoJsText
{
    private static readonly byte[] SaltedMarker = "Salted__"u8.ToArray();
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static string Encrypt(string value, string secret, string algorithm)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(8);
        byte[] cleartext = Encoding.UTF8.GetBytes(value);
        CipherParameters parameters = Derive(secret, salt, algorithm);
        byte[] ciphertext = Transform(cleartext, parameters, algorithm, encrypt: true);
        return Convert.ToBase64String([.. SaltedMarker, .. salt, .. ciphertext]);
    }

    public static string Decrypt(string value, string secret, string algorithm)
    {
        byte[] formatted = Convert.FromBase64String(value);
        byte[] salt;
        byte[] ciphertext;
        if (formatted.AsSpan().StartsWith(SaltedMarker))
        {
            if (formatted.Length < 16) throw new FormatException("Invalid encrypted text.");
            salt = formatted[8..16];
            ciphertext = formatted[16..];
        }
        else
        {
            salt = [];
            ciphertext = formatted;
        }

        CipherParameters parameters = Derive(secret, salt, algorithm);
        return StrictUtf8.GetString(Transform(ciphertext, parameters, algorithm, encrypt: false));
    }

    private static CipherParameters Derive(string secret, byte[] salt, string algorithm)
    {
        (int keyLength, int ivLength) = algorithm switch
        {
            "AES" => (32, 16),
            "TripleDES" => (24, 8),
            "Rabbit" => (16, 8),
            "RC4" => (32, 0),
            _ => throw new ArgumentException("Unknown encryption algorithm.", nameof(algorithm)),
        };

        byte[] password = Encoding.UTF8.GetBytes(secret);
        byte[] derived = new byte[keyLength + ivLength];
        byte[] previous = [];
        int offset = 0;
        while (offset < derived.Length)
        {
            byte[] input = [.. previous, .. password, .. salt];
            previous = MD5.HashData(input);
            int count = System.Math.Min(previous.Length, derived.Length - offset);
            previous.AsSpan(0, count).CopyTo(derived.AsSpan(offset));
            offset += count;
        }

        return new CipherParameters(derived[..keyLength], derived[keyLength..]);
    }

    private static byte[] Transform(byte[] input, CipherParameters parameters, string algorithm, bool encrypt) => algorithm switch
    {
        "AES" => BlockCipher(Aes.Create(), input, parameters, encrypt),
        "TripleDES" => BlockCipher(TripleDES.Create(), input, parameters, encrypt),
        "Rabbit" => Rabbit(input, parameters.Key, parameters.Iv),
        "RC4" => Rc4(input, parameters.Key),
        _ => throw new ArgumentException("Unknown encryption algorithm.", nameof(algorithm)),
    };

    private static byte[] BlockCipher(SymmetricAlgorithm cipher, byte[] input, CipherParameters parameters, bool encrypt)
    {
        using (cipher)
        {
            cipher.Mode = CipherMode.CBC;
            cipher.Padding = PaddingMode.PKCS7;
            cipher.Key = parameters.Key;
            cipher.IV = parameters.Iv;
            using ICryptoTransform transform = encrypt ? cipher.CreateEncryptor() : cipher.CreateDecryptor();
            return transform.TransformFinalBlock(input, 0, input.Length);
        }
    }

    private static byte[] Rc4(byte[] input, byte[] key)
    {
        var state = Enumerable.Range(0, 256).Select(value => (byte)value).ToArray();
        int j = 0;
        for (int i = 0; i < state.Length; i++)
        {
            j = (j + state[i] + key[i % key.Length]) & 255;
            (state[i], state[j]) = (state[j], state[i]);
        }

        var output = new byte[input.Length];
        int x = 0;
        j = 0;
        for (int index = 0; index < input.Length; index++)
        {
            x = (x + 1) & 255;
            j = (j + state[x]) & 255;
            (state[x], state[j]) = (state[j], state[x]);
            output[index] = (byte)(input[index] ^ state[(state[x] + state[j]) & 255]);
        }
        return output;
    }

    private static byte[] Rabbit(byte[] input, byte[] key, byte[] iv)
    {
        uint[] k = new uint[4];
        for (int i = 0; i < k.Length; i++) k[i] = SwapEndian(BinaryPrimitives.ReadUInt32BigEndian(key.AsSpan(i * 4)));
        uint[] x =
        [
            k[0], (k[3] << 16) | (k[2] >> 16), k[1], (k[0] << 16) | (k[3] >> 16),
            k[2], (k[1] << 16) | (k[0] >> 16), k[3], (k[2] << 16) | (k[1] >> 16),
        ];
        uint[] counters =
        [
            Rotate16(k[2]), (k[0] & 0xffff0000) | (k[1] & 0x0000ffff),
            Rotate16(k[3]), (k[1] & 0xffff0000) | (k[2] & 0x0000ffff),
            Rotate16(k[0]), (k[2] & 0xffff0000) | (k[3] & 0x0000ffff),
            Rotate16(k[1]), (k[3] & 0xffff0000) | (k[0] & 0x0000ffff),
        ];
        uint carry = 0;
        for (int i = 0; i < 4; i++) NextRabbitState(x, counters, ref carry);
        for (int i = 0; i < counters.Length; i++) counters[i] ^= x[(i + 4) & 7];

        uint iv0 = SwapEndian(BinaryPrimitives.ReadUInt32BigEndian(iv));
        uint iv2 = SwapEndian(BinaryPrimitives.ReadUInt32BigEndian(iv.AsSpan(4)));
        uint iv1 = (iv0 >> 16) | (iv2 & 0xffff0000);
        uint iv3 = (iv2 << 16) | (iv0 & 0x0000ffff);
        uint[] subvectors = [iv0, iv1, iv2, iv3, iv0, iv1, iv2, iv3];
        for (int i = 0; i < counters.Length; i++) counters[i] ^= subvectors[i];
        for (int i = 0; i < 4; i++) NextRabbitState(x, counters, ref carry);

        byte[] output = new byte[input.Length];
        Span<byte> stream = stackalloc byte[16];
        for (int offset = 0; offset < input.Length; offset += stream.Length)
        {
            NextRabbitState(x, counters, ref carry);
            uint[] words =
            [
                x[0] ^ (x[5] >> 16) ^ (x[3] << 16),
                x[2] ^ (x[7] >> 16) ^ (x[5] << 16),
                x[4] ^ (x[1] >> 16) ^ (x[7] << 16),
                x[6] ^ (x[3] >> 16) ^ (x[1] << 16),
            ];
            for (int i = 0; i < words.Length; i++) BinaryPrimitives.WriteUInt32BigEndian(stream[(i * 4)..], SwapEndian(words[i]));
            int count = System.Math.Min(stream.Length, input.Length - offset);
            for (int i = 0; i < count; i++) output[offset + i] = (byte)(input[offset + i] ^ stream[i]);
        }
        return output;
    }

    private static void NextRabbitState(uint[] x, uint[] counters, ref uint carry)
    {
        uint[] old = [.. counters];
        uint[] increments = [0x4d34d34d, 0xd34d34d3, 0x34d34d34, 0x4d34d34d, 0xd34d34d3, 0x34d34d34, 0x4d34d34d, 0xd34d34d3];
        for (int i = 0; i < counters.Length; i++)
        {
            uint priorCarry = i == 0 ? carry : counters[i - 1] < old[i - 1] ? 1u : 0u;
            counters[i] = unchecked(counters[i] + increments[i] + priorCarry);
        }
        carry = counters[7] < old[7] ? 1u : 0u;

        uint[] g = new uint[8];
        for (int i = 0; i < g.Length; i++)
        {
            uint sum = unchecked(x[i] + counters[i]);
            ulong square = (ulong)sum * sum;
            g[i] = (uint)square ^ (uint)(square >> 32);
        }

        x[0] = unchecked(g[0] + Rotate16(g[7]) + Rotate16(g[6]));
        x[1] = unchecked(g[1] + Rotate8(g[0]) + g[7]);
        x[2] = unchecked(g[2] + Rotate16(g[1]) + Rotate16(g[0]));
        x[3] = unchecked(g[3] + Rotate8(g[2]) + g[1]);
        x[4] = unchecked(g[4] + Rotate16(g[3]) + Rotate16(g[2]));
        x[5] = unchecked(g[5] + Rotate8(g[4]) + g[3]);
        x[6] = unchecked(g[6] + Rotate16(g[5]) + Rotate16(g[4]));
        x[7] = unchecked(g[7] + Rotate8(g[6]) + g[5]);
    }

    private static uint Rotate8(uint value) => (value << 8) | (value >> 24);
    private static uint Rotate16(uint value) => (value << 16) | (value >> 16);
    private static uint SwapEndian(uint value) => ((value << 8) | (value >> 24)) & 0x00ff00ff
        | ((value << 24) | (value >> 8)) & 0xff00ff00;

    private sealed record CipherParameters(byte[] Key, byte[] Iv);
}
