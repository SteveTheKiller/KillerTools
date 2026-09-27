namespace KillerTools.Engine.Network;

public sealed record ParsedUrl(string Protocol, string Hostname, string Port, string Username, string Password, string Pathname, string Search, string Hash, IReadOnlyList<string[]> SearchParams);

public static class UrlParser
{
    public static ParsedUrl Parse(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host))
        {
            throw new ArgumentException("Invalid URL.");
        }

        var userInfo = uri.UserInfo.Split(':', 2);
        var query = uri.Query;
        var parameters = query.Length == 0
            ? []
            : query[1..].Split('&').Select(pair =>
            {
                var parts = pair.Split('=', 2);
                return new[] { Decode(parts[0]), parts.Length == 2 ? Decode(parts[1]) : string.Empty };
            }).ToArray();
        return new ParsedUrl(
            $"{uri.Scheme}:", uri.Host, uri.IsDefaultPort ? string.Empty : uri.Port.ToString(),
            userInfo.Length > 0 ? userInfo[0] : string.Empty, userInfo.Length > 1 ? userInfo[1] : string.Empty,
            uri.AbsolutePath, query, uri.Fragment, parameters);
    }

    private static string Decode(string value) => Uri.UnescapeDataString(value.Replace('+', ' '));
}
