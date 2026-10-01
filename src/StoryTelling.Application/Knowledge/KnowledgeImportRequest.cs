namespace StoryTelling.Application.Knowledge;

public sealed record KnowledgeImportRequest(string Content, string Brief, int MaxChunks = 20, KnowledgeImportMode Mode = KnowledgeImportMode.Extract)
{
    public const int DefaultMaxChunks = 20;
}
