using System.IO.Compression;
using System.Reflection;
using System.Text.Json;

namespace KillerTools.Engine.Reference;

internal static class EmbeddedCatalog
{
    public static Stream OpenBrotliBase64(string resourceName)
    {
        using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"The embedded catalog {resourceName} is missing.");
        using var reader = new StreamReader(resource);
        var bytes = Convert.FromBase64String(reader.ReadToEnd());
        using var compressed = new MemoryStream(bytes);
        using var brotli = new BrotliStream(compressed, CompressionMode.Decompress);
        var output = new MemoryStream();
        brotli.CopyTo(output);
        output.Position = 0;
        return output;
    }

    public static T DeserializeBrotliBase64<T>(string resourceName)
    {
        using var stream = OpenBrotliBase64(resourceName);
        return JsonSerializer.Deserialize<T>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException($"The embedded catalog {resourceName} is invalid.");
    }
}
