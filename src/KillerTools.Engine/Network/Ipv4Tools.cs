using System.Globalization;

namespace KillerTools.Engine.Network;

public sealed record Ipv4RangeResult(string NewEnd, string NewStart, string NewCidr, long NewSize, long OldSize);

public sealed record Ipv4SubnetResult(
    string Cidr,
    string NetworkAddress,
    string NetworkMask,
    int PrefixLength,
    string WildcardMask,
    long Size,
    long UsableHosts,
    string FirstAddress,
    string LastAddress,
    string? BroadcastAddress,
    string IpClass);

public static class Ipv4Tools
{
    public static Ipv4RangeResult CoverRange(string startIp, string endIp)
    {
        var start = Parse(startIp);
        var end = Parse(endIp);
        if (start > end)
        {
            throw new ArgumentException("The starting address must not exceed the ending address.");
        }

        var differingBits = start ^ end;
        var prefix = 32;
        while (differingBits != 0)
        {
            prefix--;
            differingBits >>= 1;
        }

        var mask = PrefixMask(prefix);
        var newStart = start & mask;
        var newEnd = newStart | ~mask;
        return new Ipv4RangeResult(
            Format(newEnd),
            Format(newStart),
            $"{Format(newStart)}/{prefix}",
            (long)newEnd - newStart + 1,
            (long)end - start + 1);
    }

    public static Ipv4SubnetResult CalculateSubnet(string address)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        var parts = address.Trim().Split('/', 2);
        var ip = Parse(parts[0]);
        var prefix = parts.Length == 1 ? 32 : int.Parse(parts[1], CultureInfo.InvariantCulture);
        if (prefix is < 0 or > 32)
        {
            throw new ArgumentException("The prefix length must be from 0 through 32.");
        }

        var mask = PrefixMask(prefix);
        var network = ip & mask;
        var broadcast = network | ~mask;
        var size = 1L << (32 - prefix);
        var first = size <= 2 ? network : network + 1;
        var last = size <= 2 ? broadcast : broadcast - 1;
        return new Ipv4SubnetResult(
            $"{Format(network)}/{prefix}",
            Format(network),
            Format(mask),
            prefix,
            Format(~mask),
            size,
            size <= 2 ? (size == 2 ? 0 : 1) : size - 2,
            Format(first),
            Format(last),
            size <= 2 ? null : Format(broadcast),
            GetClass(network));
    }

    private static uint Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var parts = value.Trim().Split('.');
        if (parts.Length != 4)
        {
            throw new ArgumentException("Invalid IPv4 address.");
        }

        uint result = 0;
        foreach (var part in parts)
        {
            if (!byte.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var octet))
            {
                throw new ArgumentException("Invalid IPv4 address.");
            }
            result = (result << 8) | octet;
        }
        return result;
    }

    private static uint PrefixMask(int prefix) => prefix == 0 ? 0 : uint.MaxValue << (32 - prefix);

    private static string Format(uint value) => $"{value >> 24}.{(value >> 16) & 255}.{(value >> 8) & 255}.{value & 255}";

    private static string GetClass(uint value) => (value >> 24) switch
    {
        < 128 => "A",
        < 192 => "B",
        < 224 => "C",
        < 240 => "D",
        _ => "E",
    };
}
