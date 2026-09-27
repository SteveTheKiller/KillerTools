namespace KillerTools.Engine.Text;

public static class LoremIpsum
{
    private const string Opening = "Lorem ipsum dolor sit amet, consectetur adipiscing elit.";
    private static readonly string[] Vocabulary = "a ac accumsan ad adipiscing aenean aliquam aliquet amet ante aptent arcu at auctor bibendum blandit class commodo condimentum congue consectetur consequat conubia convallis cras cubilia cum curabitur curae dapibus diam dictum dictumst dignissim dolor donec dui duis egestas eget eleifend elementum elit enim erat eros est et etiam eu euismod facilisi faucibus felis fermentum feugiat fringilla fusce gravida habitant habitasse hac hendrerit himenaeos iaculis id imperdiet in inceptos integer interdum ipsum justo lacinia lacus laoreet lectus leo ligula litora lobortis lorem luctus maecenas magna magnis malesuada massa mattis mauris metus mi molestie mollis montes morbi mus nam nascetur natoque nec neque netus nisi nisl non nostra nulla nullam nunc odio orci ornare parturient pellentesque penatibus per pharetra phasellus placerat platea porta porttitor posuere potenti praesent pretium primis proin pulvinar purus quam quis quisque rhoncus ridiculus risus rutrum sagittis sapien scelerisque sed sem semper senectus sit sociis sociosqu sodales sollicitudin suscipit suspendisse taciti tellus tempor tempus tincidunt torquent tortor turpis ullamcorper ultrices ultricies urna varius vehicula vel velit venenatis vestibulum vitae vivamus viverra volutpat vulputate".Split(' ');

    public static string Generate(int paragraphCount = 1, int sentencesPerParagraph = 3, int wordCount = 10, bool startWithLoremIpsum = true, bool asHtml = false)
    {
        if (paragraphCount is < 1 or > 10) throw new ArgumentOutOfRangeException(nameof(paragraphCount));
        if (sentencesPerParagraph is < 1 or > 10) throw new ArgumentOutOfRangeException(nameof(sentencesPerParagraph));
        if (wordCount is < 1 or > 20) throw new ArgumentOutOfRangeException(nameof(wordCount));

        var paragraphs = Enumerable.Range(0, paragraphCount)
            .Select(_ => Enumerable.Range(0, sentencesPerParagraph).Select(_ => Sentence(wordCount)).ToArray())
            .ToArray();
        if (startWithLoremIpsum) paragraphs[0][0] = Opening;
        var text = string.Join("\n\n", paragraphs.Select(paragraph => string.Join(' ', paragraph)));
        return asHtml ? $"<p>{text.Replace("\n\n", "</p>\n\n<p>", StringComparison.Ordinal)}</p>" : text;
    }

    private static string Sentence(int wordCount)
    {
        var words = Enumerable.Range(0, wordCount).Select(_ => Vocabulary[Random.Shared.Next(Vocabulary.Length)]).ToArray();
        words[0] = char.ToUpperInvariant(words[0][0]) + words[0][1..];
        return string.Join(' ', words) + ".";
    }
}
