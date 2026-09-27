using System.Reflection;
using System.Security.Cryptography;

namespace KillerTools.Engine.Security;

public static class PasswordGenerator
{
    private const string Uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string Lowercase = "abcdefghijklmnopqrstuvwxyz";
    private const string Numbers = "0123456789";
    private const string Symbols = "!@#$%^&*()-_=+[]{};:,.<>/?";
    private const string Ambiguous = "0O1lI|`'\".,;:{}[]()\\/";
    private static readonly string[] Words = LoadWords();
    public static int WordCount => Words.Length;

    public static string GenerateRandom(int length, bool uppercase, bool lowercase, bool numbers, bool symbols, bool excludeAmbiguous, bool requireOneOfEach)
    {
        if (length is < 8 or > 128) throw new ArgumentOutOfRangeException(nameof(length));
        var pools = new[] { uppercase ? Uppercase : "", lowercase ? Lowercase : "", numbers ? Numbers : "", symbols ? Symbols : "" }
            .Select(pool => excludeAmbiguous ? new string(pool.Where(character => !Ambiguous.Contains(character)).ToArray()) : pool)
            .Where(pool => pool.Length > 0).ToArray();
        if (pools.Length == 0) throw new InvalidOperationException("Select at least one character set");
        var alphabet = string.Concat(pools);
        var characters = requireOneOfEach ? pools.Select(Pick).ToList() : [];
        while (characters.Count < length) characters.Add(Pick(alphabet));
        Shuffle(characters);
        return new string(characters.ToArray());
    }

    public static string GeneratePassphrase(int wordCount, string separator, bool capitalizeWords, bool appendNumber)
    {
        if (wordCount is < 1 or > 16 || separator.Length > 8) throw new ArgumentOutOfRangeException();
        var words = Enumerable.Range(0, wordCount).Select(_ => Words[RandomNumberGenerator.GetInt32(Words.Length)]).Select(word => capitalizeWords ? char.ToUpperInvariant(word[0]) + word[1..] : word).ToList();
        if (appendNumber) words.Add(RandomNumberGenerator.GetInt32(100).ToString("00", System.Globalization.CultureInfo.InvariantCulture));
        return string.Join(separator, words);
    }

    public static string GeneratePronounceable(int length, bool appendNumber)
    {
        if (length is < 8 or > 128) throw new ArgumentOutOfRangeException(nameof(length));
        const string consonants = "bcdfghjkmnpqrstvwxz";
        const string vowels = "aeiouy";
        var password = new string(Enumerable.Range(0, length).Select(index => Pick(index % 2 == 0 ? consonants : vowels)).ToArray());
        return appendNumber ? password[..^2] + RandomNumberGenerator.GetInt32(100).ToString("00", System.Globalization.CultureInfo.InvariantCulture) : password;
    }

    public static string GenerateFormatted(int length, string format)
    {
        if (length is < 8 or > 128) throw new ArgumentOutOfRangeException(nameof(length));
        if (format == "uuid") return Guid.NewGuid().ToString();
        var bytes = RandomNumberGenerator.GetBytes(format == "hex" ? (length + 1) / 2 : length);
        var value = format switch
        {
            "hex" => Convert.ToHexStringLower(bytes),
            "base64" => Convert.ToBase64String(bytes),
            "base64url" => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_'),
            _ => throw new ArgumentException("Unknown password format.", nameof(format)),
        };
        return value[..System.Math.Min(length, value.Length)];
    }

    private static char Pick(string pool) => pool[RandomNumberGenerator.GetInt32(pool.Length)];
    private static void Shuffle(List<char> values)
    {
        for (var index = values.Count - 1; index > 0; index--)
        {
            var swap = RandomNumberGenerator.GetInt32(index + 1);
            (values[index], values[swap]) = (values[swap], values[index]);
        }
    }

    private static string[] LoadWords()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("KillerTools.Engine.Data.eff-long-wordlist.txt") ?? throw new InvalidOperationException("EFF word list is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
    }
}
