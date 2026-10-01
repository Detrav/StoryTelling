namespace StoryTelling.Domain;

public sealed class EditorNote
{
    public EditorNoteKind Kind { get; set; } = EditorNoteKind.Other;

    public string Text { get; set; } = string.Empty;
}
