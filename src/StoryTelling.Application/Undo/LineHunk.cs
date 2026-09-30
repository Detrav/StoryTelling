namespace StoryTelling.Application.Undo;

public sealed class LineHunk
{
    public LineHunk(int oldStart, int newStart, IReadOnlyList<string> oldLines, IReadOnlyList<string> newLines)
    {
        OldStart = oldStart;
        NewStart = newStart;
        OldLines = oldLines;
        NewLines = newLines;
    }

    public int OldStart { get; }

    public int NewStart { get; }

    public IReadOnlyList<string> OldLines { get; }

    public IReadOnlyList<string> NewLines { get; }
}
