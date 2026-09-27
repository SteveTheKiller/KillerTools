using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace KillerTools.Engine.Network;

public sealed record Ipv6UlaResult(string Ula48, string FirstRoutable64, string LastRoutable64, long TimestampMs);

public static class Ipv6Ula
{
    public static Ipv6UlaResult Generate(string macAddress, long timestampMs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(macAddress);
        var input = timestampMs.ToString(CultureInfo.InvariantCulture) + macAddress;
        var hash = Convert.ToHexStringLower(SHA1.HashData(Encoding.UTF8.GetBytes(input)));
        var hex40Bit = hash[30..];
        var prefix = $"fd{hex40Bit[..2]}:{hex40Bit.Substring(2, 4)}:{hex40Bit[6..]}";
        return new Ipv6UlaResult($"{prefix}::/48", $"{prefix}:0::/64", $"{prefix}:ffff::/64", timestampMs);
    }
}
