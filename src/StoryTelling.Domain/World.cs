namespace StoryTelling.Domain;

public sealed class World
{
    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public List<string> Tags { get; set; } = [];

    public string Genre { get; set; } = string.Empty;

    public string Tone { get; set; } = string.Empty;

    public string Style { get; set; } = string.Empty;

    public string PointOfView { get; set; } = string.Empty;

    public string Tense { get; set; } = string.Empty;

    public string Rating { get; set; } = string.Empty;
}
