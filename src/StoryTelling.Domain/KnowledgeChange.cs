namespace StoryTelling.Domain;

public sealed class KnowledgeChange
{
    public KnowledgeChangeOperation Operation { get; set; }

    public KnowledgeKind Kind { get; set; } = KnowledgeKind.Note;

    public string Title { get; set; } = string.Empty;

    public List<string> Tags { get; set; } = [];

    public string Content { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;
}
