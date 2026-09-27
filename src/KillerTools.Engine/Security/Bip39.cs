using System.Reflection;
using System.Security.Cryptography;

namespace KillerTools.Engine.Security;

public static class Bip39
{
    private sealed record WordList(string Spacer, string[] Words, IReadOnlyDictionary<string, int> Index);
    private static readonly IReadOnlyDictionary<string, WordList> Lists = LoadLists();
    public static IReadOnlyCollection<string> Languages => Lists.Keys.ToArray();

    public static string GenerateEntropy() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));

    public static string EntropyToMnemonic(string entropy, string language = "English")
    {
        if (entropy.Length is < 16 or > 32 || entropy.Length % 4 != 0 || !entropy.All(Uri.IsHexDigit)) throw new FormatException("Invalid BIP39 entropy.");
        var bytes = Convert.FromHexString(entropy);
        var checksumLength = bytes.Length * 8 / 32;
        var hash = SHA256.HashData(bytes);
        var bits = string.Concat(bytes.Select(value => Convert.ToString(value, 2).PadLeft(8, '0'))) + Convert.ToString(hash[0], 2).PadLeft(8, '0')[..checksumLength];
        var list = GetList(language);
        return string.Join(list.Spacer, Enumerable.Range(0, bits.Length / 11).Select(index => list.Words[Convert.ToInt32(bits.Substring(index * 11, 11), 2)]));
    }

    public static string MnemonicToEntropy(string mnemonic, string language = "English")
    {
        var list = GetList(language);
        var words = mnemonic.Trim().Split([list.Spacer], StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) throw new FormatException("Invalid BIP39 mnemonic.");
        var indexes = words.Select(word => list.Index.TryGetValue(word, out var index) ? index : throw new FormatException("Invalid BIP39 word.")).ToArray();
        var bits = string.Concat(indexes.Select(index => Convert.ToString(index, 2).PadLeft(11, '0')));
        var entropyLength = 32 * (bits.Length / 33);
        if (entropyLength is < 64 or > 128 || entropyLength % 16 != 0 || bits.Length != entropyLength + entropyLength / 32) throw new FormatException("Invalid BIP39 mnemonic length.");
        var entropyBits = bits[..entropyLength];
        var checksumBits = bits[entropyLength..];
        var bytes = Enumerable.Range(0, entropyLength / 8).Select(index => Convert.ToByte(entropyBits.Substring(index * 8, 8), 2)).ToArray();
        var expected = Convert.ToString(SHA256.HashData(bytes)[0], 2).PadLeft(8, '0')[..checksumBits.Length];
        if (!CryptographicOperations.FixedTimeEquals(System.Text.Encoding.ASCII.GetBytes(checksumBits), System.Text.Encoding.ASCII.GetBytes(expected))) throw new FormatException("Invalid BIP39 checksum.");
        return Convert.ToHexStringLower(bytes);
    }

    private static WordList GetList(string language) => Lists.TryGetValue(language, out var list) ? list : throw new ArgumentException("Unknown BIP39 language.", nameof(language));

    private static IReadOnlyDictionary<string, WordList> LoadLists()
    {
        var definitions = new (string Language, string File, string Spacer)[]
        {
            ("English", "english", " "), ("Czech", "czech", " "), ("French", "french", " "), ("Italian", "italian", " "),
            ("Japanese", "japanese", "　"), ("Korean", "korean", " "), ("Portuguese", "portuguese", " "), ("Spanish", "spanish", " "),
            ("Chinese simplified", "chinese-simplified", " "), ("Chinese traditional", "chinese-traditional", " "),
        };
        return definitions.ToDictionary(definition => definition.Language, definition =>
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"KillerTools.Engine.Data.bip39-{definition.File}.txt") ?? throw new InvalidOperationException($"BIP39 {definition.Language} list is missing.");
            using var reader = new StreamReader(stream);
            var words = reader.ReadToEnd().Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            if (words.Length != 2048) throw new InvalidOperationException($"BIP39 {definition.Language} list is incomplete.");
            return new WordList(definition.Spacer, words, words.Select((word, index) => (word, index)).ToDictionary(item => item.word, item => item.index, StringComparer.Ordinal));
        }, StringComparer.Ordinal);
    }
}
