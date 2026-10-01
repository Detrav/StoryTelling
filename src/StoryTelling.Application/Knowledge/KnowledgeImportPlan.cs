namespace StoryTelling.Application.Knowledge;

public sealed record KnowledgeImportPlan(int ChunkCount, int MaxChunks)
{
    public bool TooLarge => ChunkCount > MaxChunks;
}
