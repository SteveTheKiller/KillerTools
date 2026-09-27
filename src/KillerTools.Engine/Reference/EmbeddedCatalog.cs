using System.IO.Compression;
using System.Reflection;

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
}
