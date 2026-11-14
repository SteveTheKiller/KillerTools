using System.Xml;
using System.Xml.Linq;

namespace KillerTools.Engine.Documents;

internal static class SafeXml
{
    internal const int MaximumCharacters = 16_384;

    internal static string BoundedOutput(string output)
    {
        CheckLength(output.Length);
        return output;
    }

    internal static void CheckLength(int length)
    {
        if (length > MaximumCharacters) throw new FormatException("XML output exceeds 16,384 characters.");
    }

    internal static XDocument Parse(string text)
    {
        using var reader = XmlReader.Create(new StringReader(text), new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaximumCharacters,
            MaxCharactersFromEntities = MaximumCharacters,
        });
        while (reader.Read())
            if (reader.Depth > 64) throw new XmlException("XML nesting limit exceeded.");
        using var safeReader = XmlReader.Create(new StringReader(text), reader.Settings);
        return XDocument.Load(safeReader, LoadOptions.PreserveWhitespace);
    }
}
