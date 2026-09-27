using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace KillerTools.Engine.Security;

public readonly record struct OtpSecret(string Secret, string Uri);

public static class OtpTools
{
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string GenerateCode(string secret, long counter)
    {
        if (counter < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(counter));
        }

        Span<byte> message = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(message, (ulong)counter);
        var digest = HMACSHA1.HashData(DecodeBase32(secret), message);
        var offset = digest[^1] & 15;
        var value = BinaryPrimitives.ReadUInt32BigEndian(digest.AsSpan(offset, 4)) & 0x7fff_ffff;
        return (value % 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    public static bool VerifyCode(string secret, long counter, string code) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(GenerateCode(secret, counter)), Encoding.ASCII.GetBytes(code));

    public static OtpSecret GenerateSecret(string issuer, string account)
    {
        var secret = string.Create(16, 0, static (buffer, _) =>
        {
            for (var index = 0; index < buffer.Length; index++)
            {
                buffer[index] = Base32Alphabet[RandomNumberGenerator.GetInt32(Base32Alphabet.Length)];
            }
        });
        var encodedIssuer = Uri.EscapeDataString(issuer);
        var encodedAccount = Uri.EscapeDataString(account);
        var uri = $"otpauth://totp/{encodedIssuer}:{encodedAccount}?issuer={encodedIssuer}&secret={secret}&algorithm=SHA1&digits=6&period=30";
        return new(secret, uri);
    }

    private static byte[] DecodeBase32(string value)
    {
        var normalized = value.Trim().TrimEnd('=').ToUpperInvariant();
        if (normalized.Length == 0 || normalized.Any(character => !Base32Alphabet.Contains(character)))
        {
            throw new FormatException("Invalid Base32 secret");
        }

        var bytes = new List<byte>(normalized.Length * 5 / 8);
        var bits = 0;
        var count = 0;
        foreach (var character in normalized)
        {
            bits = (bits << 5) | Base32Alphabet.IndexOf(character);
            count += 5;
            if (count < 8)
            {
                continue;
            }
            count -= 8;
            bytes.Add((byte)(bits >> count));
            bits &= (1 << count) - 1;
        }
        return [.. bytes];
    }
}
