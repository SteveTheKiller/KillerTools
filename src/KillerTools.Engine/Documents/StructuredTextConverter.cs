using System.Collections;
using System.Text.Encodings.Web;
using System.Text.Json;
using Tomlyn;
using Tomlyn.Model;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace KillerTools.Engine.Documents;

public static class StructuredTextConverter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        IndentCharacter = ' ',
        IndentSize = 2,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithAttemptingUnquotedStringTypeDeserialization()
        .WithDuplicateKeyChecking()
        .Build();
    private static readonly ISerializer YamlSerializer = new SerializerBuilder()
        .DisableAliases()
        .WithIndentedSequences()
        .WithQuotingNecessaryStrings()
        .WithNewLine("\n")
        .Build();

    public static string JsonToYaml(string text) => ToYaml(StructuredJson.Parse(text));
    public static string JsonToToml(string text) => ToToml(StructuredJson.Parse(text));

    public static string YamlToJson(string text)
    {
        var value = ParseYaml(text) ?? throw new FormatException("YAML input is empty.");
        return ToJson(value);
    }

    public static string YamlToToml(string text)
    {
        var value = ParseYaml(text) ?? throw new FormatException("YAML input is empty.");
        return ToToml(value);
    }

    public static string TomlToJson(string text) => ToJson(ParseToml(text));
    public static string TomlToYaml(string text) => ToYaml(ParseToml(text));

    public static string FormatYaml(string text, int indentSize = 2, bool sortKeys = false)
    {
        if (indentSize is < 1 or > 10) throw new ArgumentOutOfRangeException(nameof(indentSize));
        var value = ParseYaml(text);
        if (sortKeys) value = SortKeys(value);
        using var writer = new StringWriter { NewLine = "\n" };
        var settings = new EmitterSettings(indentSize, int.MaxValue, false, 1024, false, true, "\n", false);
        YamlSerializer.Serialize(new Emitter(writer, settings), value);
        return writer.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static object? ParseYaml(string text) => Normalize(YamlDeserializer.Deserialize<object>(text));
    private static object ParseToml(string text) => Normalize(TomlSerializer.Deserialize<TomlTable>(text))!;
    private static string ToJson(object? value) => JsonSerializer.Serialize(value, JsonOptions).Replace("\r\n", "\n", StringComparison.Ordinal);
    private static string ToYaml(object? value) => YamlSerializer.Serialize(value).Replace("\r\n", "\n", StringComparison.Ordinal);
    private static string ToToml(object? value) => TomlSerializer.Serialize(ToTomlValue(value)).Trim();

    private static object? SortKeys(object? value) => value switch
    {
        Dictionary<string, object?> dictionary => dictionary.OrderBy(item => item.Key, StringComparer.Ordinal).ToDictionary(item => item.Key, item => SortKeys(item.Value), StringComparer.Ordinal),
        List<object?> list => list.Select(SortKeys).ToList(),
        _ => value,
    };

    private static object? Normalize(object? value) => value switch
    {
        TomlTable table => table.ToDictionary(entry => entry.Key, entry => Normalize(entry.Value), StringComparer.Ordinal),
        IDictionary dictionary => dictionary.Keys.Cast<object?>().ToDictionary(key => key?.ToString() ?? string.Empty, key => Normalize(dictionary[key!]), StringComparer.Ordinal),
        IEnumerable sequence when value is not string => sequence.Cast<object?>().Select(Normalize).ToList(),
        _ => value,
    };

    private static object? ToTomlValue(object? value) => value switch
    {
        Dictionary<string, object?> dictionary => ToTomlTable(dictionary),
        List<object?> list => ToTomlArray(list),
        _ => value,
    };

    private static TomlTable ToTomlTable(Dictionary<string, object?> dictionary)
    {
        var table = new TomlTable();
        foreach (var item in dictionary) table[item.Key] = ToTomlValue(item.Value)!;
        return table;
    }

    private static TomlArray ToTomlArray(IEnumerable<object?> values)
    {
        var array = new TomlArray();
        foreach (var value in values) array.Add(ToTomlValue(value)!);
        return array;
    }
}
