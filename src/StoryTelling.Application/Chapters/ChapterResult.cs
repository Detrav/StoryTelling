using StoryTelling.Domain;

namespace StoryTelling.Application.Chapters;

public sealed record ChapterResult(
    string Text,
    string Logline,
    WorldState WorldState,
    IReadOnlyList<KnowledgeChange> KnowledgeChanges,
    IReadOnlyList<EditorNote> EditorNotes,
    IReadOnlyList<ContinuityIssue> ContinuityIssues,
    IReadOnlyList<DirectionRewrite> DirectionRewrites,
    int ToolCalls)
{
    public EditorVerdict Verdict { get; init; } = EditorVerdict.Ok;
}
