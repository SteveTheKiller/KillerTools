using System.Text;
using System.Text.Json;
using KillerTools.Engine.Math;
using KillerTools.Engine.Numbers;
using KillerTools.Engine.Text;

Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = Encoding.UTF8;

using var input = JsonDocument.Parse(Console.In.ReadToEnd());
var output = new List<object>();

foreach (var request in input.RootElement.EnumerateArray())
{
    var operation = request.GetProperty("operation").GetString();
    var arguments = request.GetProperty("arguments");
    output.Add(operation switch
    {
        "text_statistics" => TextStatisticsResult(arguments),
        "calculate_percentage" => PercentageResult(arguments),
        "encode_base64" => new { encoded = Base64Text.Encode(arguments.GetProperty("text").GetString()!, arguments.TryGetProperty("urlSafe", out var encodeSafe) && encodeSafe.GetBoolean()) },
        "decode_base64" => new { text = Base64Text.Decode(arguments.GetProperty("encoded").GetString()!, arguments.TryGetProperty("urlSafe", out var decodeSafe) && decodeSafe.GetBoolean()) },
        "text_to_ascii_binary" => new { binary = AsciiBinary.Encode(arguments.GetProperty("text").GetString()!) },
        "ascii_binary_to_text" => new { text = AsciiBinary.Decode(arguments.GetProperty("binary").GetString()!) },
        "arabic_to_roman" => new { roman = RomanNumerals.FromArabic(arguments.GetProperty("number").GetInt32()) },
        "roman_to_arabic" => new { number = RomanNumerals.ToArabic(arguments.GetProperty("roman").GetString()!) },
        "text_to_nato_alphabet" => new { nato = NatoAlphabet.Convert(arguments.GetProperty("text").GetString()!) },
        _ => throw new InvalidOperationException($"Unknown parity operation: {operation}"),
    });
}

Console.Write(JsonSerializer.Serialize(output));

static object TextStatisticsResult(JsonElement arguments)
{
    var value = TextStatistics.Calculate(arguments.GetProperty("text").GetString()!);
    return new
    {
        charCount = value.CharacterCount,
        wordCount = value.WordCount,
        lineCount = value.LineCount,
        byteSize = value.ByteSize,
    };
}

static object PercentageResult(JsonElement arguments)
{
    var mode = arguments.GetProperty("mode").GetString();
    var x = arguments.GetProperty("x").GetDouble();
    var y = arguments.GetProperty("y").GetDouble();
    var value = mode switch
    {
        "percent_of" => Percentages.Of(x, y),
        "what_percent" => Percentages.Ratio(x, y),
        "change" => Percentages.Change(x, y),
        _ => throw new InvalidOperationException($"Unknown percentage mode: {mode}"),
    };
    return new { value };
}
