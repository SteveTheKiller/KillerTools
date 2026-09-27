using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace KillerTools.Engine.Security;

public static class IdentifierGenerators
{
    private const string UlidAlphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private static readonly DateTimeOffset GregorianEpoch = new(1582, 10, 15, 0, 0, 0, TimeSpan.Zero);

    public static IReadOnlyList<string> GenerateUlids(int count) =>
        Enumerable.Range(0, count).Select(_ => GenerateUlid()).ToArray();

    public static IReadOnlyList<string> GenerateUuids(string version, int count, string? name = null, string? namespaceValue = null)
    {
        if (count is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        Guid namespaceId = default;
        if (version is "v3" or "v5" && !Guid.TryParse(namespaceValue, out namespaceId))
        {
            throw new FormatException("A valid namespace UUID is required");
        }

        return Enumerable.Range(0, count).Select(index => version switch
        {
            "NIL" => Guid.Empty.ToString(),
            "v1" => GenerateVersion1(index),
            "v3" => GenerateNameBased(namespaceId, name ?? "", 3),
            "v4" => Guid.NewGuid().ToString(),
            "v5" => GenerateNameBased(namespaceId, name ?? "", 5),
            _ => throw new ArgumentException("Unknown UUID version", nameof(version)),
        }).ToArray();
    }

    private static string GenerateUlid()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Span<byte> random = stackalloc byte[10];
        RandomNumberGenerator.Fill(random);
        UInt128 value = (ulong)timestamp;
        foreach (var item in random)
        {
            value = (value << 8) | item;
        }
        Span<char> output = stackalloc char[26];
        for (var index = output.Length - 1; index >= 0; index--)
        {
            output[index] = UlidAlphabet[(int)(value & 31)];
            value >>= 5;
        }
        return new(output);
    }

    private static string GenerateVersion1(int clockSequence)
    {
        var timestamp = (ulong)((DateTimeOffset.UtcNow - GregorianEpoch).Ticks);
        Span<byte> bytes = stackalloc byte[16];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, (uint)timestamp);
        BinaryPrimitives.WriteUInt16BigEndian(bytes[4..], (ushort)(timestamp >> 32));
        BinaryPrimitives.WriteUInt16BigEndian(bytes[6..], (ushort)(((timestamp >> 48) & 0x0fff) | 0x1000));
        bytes[8] = (byte)(((clockSequence >> 8) & 0x3f) | 0x80);
        bytes[9] = (byte)clockSequence;
        RandomNumberGenerator.Fill(bytes[10..]);
        return new Guid(bytes, bigEndian: true).ToString();
    }

    private static string GenerateNameBased(Guid namespaceId, string name, int version)
    {
        Span<byte> namespaceBytes = stackalloc byte[16];
        namespaceId.TryWriteBytes(namespaceBytes, bigEndian: true, out _);
        var nameBytes = Encoding.UTF8.GetBytes(name);
        var input = new byte[namespaceBytes.Length + nameBytes.Length];
        namespaceBytes.CopyTo(input);
        nameBytes.CopyTo(input.AsSpan(namespaceBytes.Length));
        var hash = version == 3 ? MD5.HashData(input) : SHA1.HashData(input);
        hash[6] = (byte)((hash[6] & 0x0f) | (version << 4));
        hash[8] = (byte)((hash[8] & 0x3f) | 0x80);
        return new Guid(hash.AsSpan(0, 16), bigEndian: true).ToString();
    }
}
