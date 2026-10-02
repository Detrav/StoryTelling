namespace StoryTelling.Domain;

public sealed class Chapter
{
    public int Number { get; set; }

    public string Title { get; set; } = string.Empty;

    public ChapterRole Role { get; set; } = ChapterRole.Auto;

    public string Direction { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public string ContentOriginal { get; set; } = string.Empty;

    public SortedDictionary<string, string> Translations { get; set; } = [];

    public SortedDictionary<string, string> TranslatedTitles { get; set; } = [];

    public List<string> StaleTranslations { get; set; } = [];

    public string Logline { get; set; } = string.Empty;

    public string StorySoFar { get; set; } = string.Empty;

    public WorldState? WorldState { get; set; }

    public List<KnowledgeChange> KnowledgeChanges { get; set; } = [];

    public List<EditorNote> EditorNotes { get; set; } = [];

    public ChapterStatus Status { get; set; } = ChapterStatus.Draft;

    public DateTimeOffset CreatedUtc { get; set; }
}
