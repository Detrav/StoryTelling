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
    public EditorChecklistVerdict Checklist { get; init; } = EditorChecklistVerdict.Empty;
}
