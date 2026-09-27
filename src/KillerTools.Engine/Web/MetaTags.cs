namespace KillerTools.Engine.Web;

public static class MetaTags
{
    private static readonly string[] CommonFields =
    [
        "type", "title", "description", "url", "image", "image:alt", "image:width", "image:height",
        "twitter:card", "twitter:site", "twitter:creator",
    ];

    private static readonly IReadOnlyDictionary<string, string[]> TypeFields = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["website"] = [],
        ["article"] = ["article:published_time", "article:modified_time", "article:expiration_time", "article:author", "article:section", "article:tag"],
        ["book"] = ["book:author", "book:isbn", "book:release_date", "book:tag"],
        ["profile"] = ["profile:first_name", "profile:last_name", "profile:username", "profile:gender"],
        ["music.song"] = ["music:duration", "music:album", "music:album:disk", "music:album:track", "music:musician"],
        ["music.album"] = ["music:song", "music:song:disc", "music:song:track", "music:musician", "music:release_date"],
        ["music.playlist"] = ["music:song", "music:song:disc", "music:song:track", "music:creator"],
        ["music.radio_station"] = ["music:creator"],
        ["video.movie"] = ["video:actor", "video:director", "video:writer", "video:duration", "video:release_date", "video:tag"],
        ["video.episode"] = ["video:actor", "video:director", "video:writer", "video:duration", "video:release_date", "video:tag", "video:series"],
        ["video.tv_show"] = ["video:actor", "video:director", "video:writer", "video:duration", "video:release_date", "video:tag"],
        ["video.other"] = ["video:actor", "video:director", "video:writer", "video:duration", "video:release_date", "video:tag"],
    };

    public static string Generate(string type, IEnumerable<KeyValuePair<string, string>> fields)
    {
        if (!TypeFields.TryGetValue(type, out var typeFields)) throw new ArgumentException("Unknown page type.", nameof(type));
        var supplied = fields.ToList();
        if (supplied.Count > 30) throw new ArgumentException("Too many metadata fields", nameof(fields));
        var allowed = CommonFields.Concat(typeFields).ToHashSet(StringComparer.Ordinal);
        if (supplied.Any(field => field.Key.Length > 64 || field.Value.Length > 1024 || !allowed.Contains(field.Key)))
        {
            throw new ArgumentException("Unknown metadata field for selected page type", nameof(fields));
        }

        var metadata = new List<KeyValuePair<string, string>>
        {
            new("type", type),
            new("twitter:card", "summary_large_image"),
        };
        foreach (var field in supplied)
        {
            var index = metadata.FindIndex(item => item.Key == field.Key);
            if (index >= 0) metadata[index] = field;
            else metadata.Add(field);
        }

        var openGraph = metadata.Where(item => !item.Key.StartsWith("twitter:", StringComparison.Ordinal) && item.Value.Length > 0).ToList();
        var twitter = metadata.Where(item => item.Key.StartsWith("twitter:", StringComparison.Ordinal) && item.Value.Length > 0)
            .Select(item => new KeyValuePair<string, string>(item.Key[8..], item.Value)).ToList();
        var explicitTwitterKeys = twitter.Select(item => item.Key).ToHashSet(StringComparer.Ordinal);
        var compatibility = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["description"] = "description",
            ["title"] = "title",
            ["image"] = "image",
            ["image:url"] = "image",
            ["image:alt"] = "image:alt",
        };
        foreach (var item in openGraph)
        {
            if (compatibility.TryGetValue(item.Key, out var twitterKey) && !explicitTwitterKeys.Contains(twitterKey))
            {
                twitter.Add(new(twitterKey, item.Value));
            }
        }

        var groups = new List<string>();
        if (openGraph.Count > 0) groups.Add(BuildGroup("og meta", "property", "og:", openGraph));
        if (twitter.Count > 0) groups.Add(BuildGroup("twitter meta", "name", "twitter:", twitter));
        return string.Join("\n\n", groups);
    }

    private static string BuildGroup(string title, string attribute, string prefix, IEnumerable<KeyValuePair<string, string>> fields)
    {
        var lines = new List<string> { $"<!-- {title} -->" };
        lines.AddRange(fields.Select(field => $"<meta {attribute}=\"{prefix}{field.Key.Trim()}\" value=\"{field.Value.Trim()}\" />"));
        return string.Join('\n', lines);
    }
}
