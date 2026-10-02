using StoryTelling.Domain;

namespace StoryTelling.Application.Chapters;

public sealed record ChapterResult(
    string Text,
    string Logline,
    WorldState WorldState,
    IReadOnlyList<KnowledgeChange> KnowledgeChanges,
    IReadOnlyList<EditorNote> EditorNotes,
    int ToolCalls,
    string StorySoFar = "");
