using System.Linq;

namespace StoryTelling.Application.Undo;

public sealed class TextPatch
{
    public static TextPatch Empty { get; } = new([]);

    public TextPatch(IReadOnlyList<LineHunk> hunks) => Hunks = hunks;

    public IReadOnlyList<LineHunk> Hunks { get; }

    public bool IsEmpty => Hunks.Count == 0;

    public string Apply(string text)
    {
        var lines = SplitLines(text);

        foreach (var hunk in Hunks.OrderByDescending(hunk => hunk.OldStart))
        {
            lines.RemoveRange(hunk.OldStart, hunk.OldLines.Count);
            lines.InsertRange(hunk.OldStart, hunk.NewLines);
        }

        return JoinLines(lines);
    }

    public string Revert(string text)
    {
        var lines = SplitLines(text);

        foreach (var hunk in Hunks.OrderByDescending(hunk => hunk.NewStart))
        {
            lines.RemoveRange(hunk.NewStart, hunk.NewLines.Count);
            lines.InsertRange(hunk.NewStart, hunk.OldLines);
        }

        return JoinLines(lines);
    }

    internal static List<string> SplitLines(string text) =>
        text.Length == 0 ? [] : [.. text.Split('\n')];

    internal static string JoinLines(List<string> lines) => string.Join('\n', lines);
}
