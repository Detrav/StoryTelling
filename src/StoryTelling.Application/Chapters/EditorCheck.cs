namespace StoryTelling.Application.Chapters;

public sealed record EditorCheck(
    string Id,
    string Label,
    string Question,
    string FixInstruction);
