using StoryTelling.Domain;

namespace StoryTelling.Application.Chapters;

public sealed record ChapterEdit(
    string Text,
    IReadOnlyList<EditorNote> Notes,
    EditorVerdict Verdict);
