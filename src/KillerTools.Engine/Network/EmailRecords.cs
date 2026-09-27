namespace KillerTools.Engine.Network;

public static class EmailRecords
{
    public static string BuildSpf(IEnumerable<string> providers, IEnumerable<string> ipAddresses, string enforcement)
    {
        var parts = new List<string> { "v=spf1" };
        parts.AddRange(providers.Distinct(StringComparer.Ordinal));
        parts.AddRange(ipAddresses.Select(ip => ip.Contains(':', StringComparison.Ordinal) ? $"ip6:{ip}" : $"ip4:{ip}"));
        parts.Add(enforcement);
        return string.Join(' ', parts);
    }

    public static string BuildDmarc(string policy, string subdomainPolicy, int percentage, IEnumerable<string> ruaEmails, IEnumerable<string> rufEmails, string adkim, string aspf)
    {
        var parts = new List<string> { $"v=DMARC1; p={policy}" };
        if (subdomainPolicy.Length > 0) parts.Add($"sp={subdomainPolicy}");
        if (percentage < 100) parts.Add($"pct={percentage}");
        AddAddresses(parts, "rua", ruaEmails);
        AddAddresses(parts, "ruf", rufEmails);
        if (adkim.Length > 0) parts.Add($"adkim={adkim}");
        if (aspf.Length > 0) parts.Add($"aspf={aspf}");
        return string.Join("; ", parts);
    }

    private static void AddAddresses(List<string> parts, string tag, IEnumerable<string> addresses)
    {
        var value = string.Join(',', addresses.Select(address => $"mailto:{(address.StartsWith("mailto:", StringComparison.Ordinal) ? address[7..] : address)}"));
        if (value.Length > 0) parts.Add($"{tag}={value}");
    }
}
