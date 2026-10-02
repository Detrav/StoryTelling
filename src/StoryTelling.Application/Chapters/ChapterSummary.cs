using StoryTelling.Domain;

namespace StoryTelling.Application.Chapters;

public sealed record DirectionRewrite(int ChapterNumber, string Direction);

public sealed record ChapterSummary(
    string Logline,
    WorldState WorldState,
    IReadOnlyList<KnowledgeChange> KnowledgeChanges,
    IReadOnlyList<ContinuityIssue> ContinuityIssues,
    IReadOnlyList<DirectionRewrite> DirectionRewrites);
