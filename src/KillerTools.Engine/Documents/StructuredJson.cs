using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace KillerTools.Engine.Documents;

public static class StructuredJson
{
    private static readonly JsonSerializerOptions CompactOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static object? Parse(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var parser = new Json5Parser(text);
        var value = parser.ParseValue();
        parser.RequireEnd();
        return value;
    }

    public static string Minify(string text) => Serialize(Parse(text), 0);

    public static string Format(string text, int indentSize = 3, bool sortKeys = true)
    {
        if (indentSize is < 0 or > 10) throw new ArgumentOutOfRangeException(nameof(indentSize));
        var value = Parse(text);
        return Serialize(sortKeys ? SortKeys(value) : value, indentSize);
    }

    public static object Diff(string left, string right, bool onlyDifferences = true) =>
        Difference(string.Empty, Parse(left), true, Parse(right), true, onlyDifferences);

    private static string Serialize(object? value, int indentSize)
    {
        if (indentSize == 0) return JsonSerializer.Serialize(value, CompactOptions);
        var options = new JsonSerializerOptions(CompactOptions)
        {
            WriteIndented = true,
            IndentCharacter = ' ',
            IndentSize = indentSize,
        };
        return JsonSerializer.Serialize(value, options).Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static object? SortKeys(object? value) => value switch
    {
        Dictionary<string, object?> dictionary => dictionary.OrderBy(item => item.Key, StringComparer.CurrentCulture)
            .ToDictionary(item => item.Key, item => SortKeys(item.Value), StringComparer.Ordinal),
        List<object?> list => list.Select(SortKeys).ToList(),
        _ => value,
    };

    private static Dictionary<string, object?> Difference(object key, object? oldValue, bool hasOld, object? value, bool hasNew, bool onlyDifferences)
    {
        var type = TypeOf(hasOld ? oldValue : value);
        var result = new Dictionary<string, object?>
        {
            ["key"] = key,
            ["type"] = type,
        };
        if (type == "object") result["children"] = ObjectDifferences(oldValue as Dictionary<string, object?>, value as Dictionary<string, object?>, onlyDifferences);
        else if (type == "array") result["children"] = ArrayDifferences(oldValue as List<object?>, value as List<object?>, onlyDifferences);
        if (hasOld) result["oldValue"] = oldValue;
        if (hasNew) result["value"] = value;
        result["status"] = Status(oldValue, hasOld, value, hasNew);
        return result;
    }

    private static List<object> ObjectDifferences(Dictionary<string, object?>? oldValue, Dictionary<string, object?>? value, bool onlyDifferences)
    {
        oldValue ??= [];
        value ??= [];
        var keys = oldValue.Keys.Concat(value.Keys).Distinct(StringComparer.Ordinal);
        return keys.Select(key => Difference(key, oldValue.GetValueOrDefault(key), oldValue.ContainsKey(key), value.GetValueOrDefault(key), value.ContainsKey(key), onlyDifferences))
            .Where(item => !onlyDifferences || !Equals(item["status"], "unchanged")).Cast<object>().ToList();
    }

    private static List<object> ArrayDifferences(List<object?>? oldValue, List<object?>? value, bool onlyDifferences)
    {
        oldValue ??= [];
        value ??= [];
        return Enumerable.Range(0, System.Math.Max(oldValue.Count, value.Count))
            .Select(index => Difference(index, index < oldValue.Count ? oldValue[index] : null, index < oldValue.Count, index < value.Count ? value[index] : null, index < value.Count, onlyDifferences))
            .Where(item => !onlyDifferences || !Equals(item["status"], "unchanged")).Cast<object>().ToList();
    }

    private static string Status(object? oldValue, bool hasOld, object? value, bool hasNew)
    {
        if (!hasOld) return "added";
        if (!hasNew) return "removed";
        if (DeepEquals(oldValue, value)) return "unchanged";
        return TypeOf(oldValue) == TypeOf(value) && TypeOf(oldValue) is "object" or "array" ? "children-updated" : "updated";
    }

    private static string TypeOf(object? value) => value switch
    {
        Dictionary<string, object?> => "object",
        List<object?> => "array",
        _ => "value",
    };

    private static bool DeepEquals(object? left, object? right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is Dictionary<string, object?> leftObject && right is Dictionary<string, object?> rightObject)
            return leftObject.Count == rightObject.Count && leftObject.All(item => rightObject.TryGetValue(item.Key, out var other) && DeepEquals(item.Value, other));
        if (left is List<object?> leftArray && right is List<object?> rightArray)
            return leftArray.Count == rightArray.Count && leftArray.Zip(rightArray).All(pair => DeepEquals(pair.First, pair.Second));
        if (left is double leftNumber && right is double rightNumber)
            return leftNumber.Equals(rightNumber) || double.IsNaN(leftNumber) && double.IsNaN(rightNumber);
        return Equals(left, right);
    }

    private sealed class Json5Parser(string text)
    {
        private int _index;

        public object? ParseValue()
        {
            SkipTrivia();
            if (_index >= text.Length) throw Invalid();
            return text[_index] switch
            {
                '{' => ParseObject(),
                '[' => ParseArray(),
                '\'' or '"' => ParseString(),
                _ => ParseAtom(),
            };
        }

        public void RequireEnd()
        {
            SkipTrivia();
            if (_index != text.Length) throw Invalid();
        }

        private Dictionary<string, object?> ParseObject()
        {
            _index++;
            var result = new Dictionary<string, object?>(StringComparer.Ordinal);
            SkipTrivia();
            if (Take('}')) return result;
            while (true)
            {
                SkipTrivia();
                var key = _index < text.Length && text[_index] is '\'' or '"' ? ParseString() : ParseIdentifier();
                SkipTrivia();
                Require(':');
                result[key] = ParseValue();
                SkipTrivia();
                if (Take('}')) return result;
                Require(',');
                SkipTrivia();
                if (Take('}')) return result;
            }
        }

        private List<object?> ParseArray()
        {
            _index++;
            var result = new List<object?>();
            SkipTrivia();
            if (Take(']')) return result;
            while (true)
            {
                result.Add(ParseValue());
                SkipTrivia();
                if (Take(']')) return result;
                Require(',');
                SkipTrivia();
                if (Take(']')) return result;
            }
        }

        private string ParseString()
        {
            var quote = text[_index++];
            var output = new StringBuilder();
            while (_index < text.Length)
            {
                var character = text[_index++];
                if (character == quote) return output.ToString();
                if (character != '\\')
                {
                    if (character is '\r' or '\n') throw Invalid();
                    output.Append(character);
                    continue;
                }
                if (_index >= text.Length) throw Invalid();
                var escaped = text[_index++];
                if (escaped == '\r') { if (_index < text.Length && text[_index] == '\n') _index++; continue; }
                if (escaped == '\n') continue;
                output.Append(escaped switch
                {
                    'b' => '\b', 'f' => '\f', 'n' => '\n', 'r' => '\r', 't' => '\t', 'v' => '\v', '0' => '\0',
                    'x' => (char)ParseHex(2), 'u' => (char)ParseHex(4),
                    _ => escaped,
                });
            }
            throw Invalid();
        }

        private object? ParseAtom()
        {
            var start = _index;
            while (_index < text.Length && !char.IsWhiteSpace(text[_index]) && text[_index] is not ',' and not ']' and not '}') _index++;
            var token = text[start.._index];
            return token switch
            {
                "true" => true,
                "false" => false,
                "null" => null,
                "Infinity" or "+Infinity" or "-Infinity" or "NaN" or "+NaN" or "-NaN" => null,
                _ => ParseNumber(token),
            };
        }

        private static object ParseNumber(string token)
        {
            var sign = 1;
            if (token.StartsWith('+')) token = token[1..];
            else if (token.StartsWith('-')) { sign = -1; token = token[1..]; }
            if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                var number = Convert.ToInt64(token[2..], 16) * sign;
                return number;
            }
            var signed = sign < 0 ? "-" + token : token;
            if (long.TryParse(signed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer)) return integer;
            if (double.TryParse(signed, NumberStyles.Float, CultureInfo.InvariantCulture, out var floating)) return floating == 0 ? 0d : floating;
            throw Invalid();
        }

        private string ParseIdentifier()
        {
            var start = _index;
            if (_index >= text.Length || !(char.IsLetter(text[_index]) || text[_index] is '_' or '$')) throw Invalid();
            _index++;
            while (_index < text.Length && (char.IsLetterOrDigit(text[_index]) || text[_index] is '_' or '$')) _index++;
            return text[start.._index];
        }

        private int ParseHex(int length)
        {
            if (_index + length > text.Length) throw Invalid();
            if (!int.TryParse(text.AsSpan(_index, length), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value)) throw Invalid();
            _index += length;
            return value;
        }

        private void SkipTrivia()
        {
            while (_index < text.Length)
            {
                if (char.IsWhiteSpace(text[_index])) { _index++; continue; }
                if (_index + 1 >= text.Length || text[_index] != '/') return;
                if (text[_index + 1] == '/')
                {
                    _index += 2;
                    while (_index < text.Length && text[_index] is not '\r' and not '\n') _index++;
                    continue;
                }
                if (text[_index + 1] == '*')
                {
                    var end = text.IndexOf("*/", _index + 2, StringComparison.Ordinal);
                    if (end < 0) throw Invalid();
                    _index = end + 2;
                    continue;
                }
                return;
            }
        }

        private bool Take(char character)
        {
            if (_index >= text.Length || text[_index] != character) return false;
            _index++;
            return true;
        }

        private void Require(char character)
        {
            if (!Take(character)) throw Invalid();
        }

        private static FormatException Invalid() => new("Invalid JSON5 input");
    }
}
