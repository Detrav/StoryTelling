namespace StoryTelling.Domain;

public sealed class WorldLore
{
    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public List<string> Tags { get; set; } = [];
}
