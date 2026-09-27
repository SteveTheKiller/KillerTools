using System.Text.RegularExpressions;

namespace KillerTools.Engine.Documents;

public readonly record struct EncodedFile(string Base64, int ByteLength);
public readonly record struct DecodedFile(string Path, int ByteLength);

public static partial class Base64Files
{
    public const int MaximumBytes = 32 * 1024;

    [GeneratedRegex("^(?:[a-z0-9+/]{4})*(?:[a-z0-9+/]{2}==|[a-z0-9+/]{3}=)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Base64Pattern();

    public static EncodedFile Encode(string path)
    {
        var file = new FileInfo(path);
        if (!file.Exists)
        {
            throw new FileNotFoundException("Unable to read file.", path);
        }
        if (file.Length > MaximumBytes)
        {
            throw new InvalidDataException("File exceeds 32 KiB limit");
        }

        var bytes = File.ReadAllBytes(path);
        if (bytes.Length > MaximumBytes)
        {
            throw new InvalidDataException("File exceeds 32 KiB limit");
        }
        return new(Convert.ToBase64String(bytes), bytes.Length);
    }

    public static DecodedFile Decode(string path, string base64)
    {
        if (!Base64Pattern().IsMatch(base64))
        {
            throw new FormatException("Invalid Base64");
        }

        var bytes = Convert.FromBase64String(base64);
        if (bytes.Length > MaximumBytes)
        {
            throw new InvalidDataException("Decoded file exceeds 32 KiB limit");
        }

        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(bytes);
        return new(path, bytes.Length);
    }
}
