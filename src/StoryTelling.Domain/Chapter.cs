namespace StoryTelling.Domain;

public sealed class Chapter
{
    public int Number { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Direction { get; set; } = string.Empty;

    public string ContentOriginal { get; set; } = string.Empty;

    public string? ContentTranslated { get; set; }

    public string Summary { get; set; } = string.Empty;

    public ChapterStatus Status { get; set; } = ChapterStatus.Draft;

    public DateTimeOffset CreatedUtc { get; set; }
}
