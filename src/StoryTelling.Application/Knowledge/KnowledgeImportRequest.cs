namespace StoryTelling.Application.Knowledge;

public sealed record KnowledgeImportRequest(string Content, string Brief, int MaxChunks = 20)
{
    public const int DefaultMaxChunks = 20;
}
