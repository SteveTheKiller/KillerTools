using System.Globalization;

namespace KillerTools.Engine.SystemTools;

public sealed record DateTimeConversion(string Iso8601, string Iso9075, string Rfc3339, string Rfc7231, string UnixSeconds, string TimestampMs, string Utc, string MongoObjectId, string Excel);

public static class DateTimeConverter
{
    public static DateTimeConversion Convert(string value, string inputFormat)
    {
        var instant = inputFormat switch
        {
            "unix_seconds" => DateTimeOffset.FromUnixTimeSeconds(long.Parse(value, CultureInfo.InvariantCulture)),
            "timestamp_ms" => DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(value, CultureInfo.InvariantCulture)),
            "mongo_object_id" when value.Length == 24 => DateTimeOffset.FromUnixTimeSeconds(long.Parse(value[..8], NumberStyles.HexNumber, CultureInfo.InvariantCulture)),
            "excel" => DateTimeOffset.FromUnixTimeMilliseconds((long)System.Math.Round((double.Parse(value, CultureInfo.InvariantCulture) - 25569d) * 86400000d)),
            _ => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal),
        };
        var local = instant.ToLocalTime();
        var utc = instant.ToUniversalTime();
        var unixMs = utc.ToUnixTimeMilliseconds();
        var offset = local.Offset == TimeSpan.Zero ? "Z" : local.ToString("zzz", CultureInfo.InvariantCulture);
        var localBase = local.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
        return new(localBase + offset, local.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), localBase + offset,
            utc.ToString("r", CultureInfo.InvariantCulture), utc.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            unixMs.ToString(CultureInfo.InvariantCulture), utc.ToString("r", CultureInfo.InvariantCulture),
            $"{utc.ToUnixTimeSeconds():x}0000000000000000", (unixMs / 86400000d + 25569d).ToString("G17", CultureInfo.InvariantCulture));
    }
}
