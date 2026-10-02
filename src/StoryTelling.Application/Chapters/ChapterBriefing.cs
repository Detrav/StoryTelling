using StoryTelling.Domain;

namespace StoryTelling.Application.Chapters;

public sealed record ChapterBriefing(
    string Logline,
    WorldState WorldState,
    IReadOnlyList<KnowledgeChange> KnowledgeChanges);
