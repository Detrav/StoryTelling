namespace StoryTelling.Domain;

public sealed class KnowledgeEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public KnowledgeKind Kind { get; set; } = KnowledgeKind.Note;

    public string Title { get; set; } = string.Empty;

    public List<string> Tags { get; set; } = [];

    public string Content { get; set; } = string.Empty;

    public KnowledgeStatus? Status { get; set; }
}
