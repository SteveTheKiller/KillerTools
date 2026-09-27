namespace KillerTools.Engine.Text;

public static class NatoAlphabet
{
    private static readonly string[] Words = ["Alpha", "Bravo", "Charlie", "Delta", "Echo", "Foxtrot", "Golf", "Hotel", "India", "Juliet", "Kilo", "Lima", "Mike", "November", "Oscar", "Papa", "Quebec", "Romeo", "Sierra", "Tango", "Uniform", "Victor", "Whiskey", "X-ray", "Yankee", "Zulu"];

    public static string Convert(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return string.Join(' ', text.Select(character =>
        {
            var index = char.ToLowerInvariant(character) - 'a';
            return index is >= 0 and < 26 ? Words[index] : character == ' ' ? string.Empty : character.ToString();
        }).Where(value => value.Length != 0));
    }
}
