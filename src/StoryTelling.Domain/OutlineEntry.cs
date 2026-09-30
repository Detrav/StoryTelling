namespace StoryTelling.Domain;

public sealed class OutlineEntry
{
    public int ChapterNumber { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Direction { get; set; } = string.Empty;

    public bool Approved { get; set; }
}
