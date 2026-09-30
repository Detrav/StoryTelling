using System.Linq;
using DiffPlex;
using StoryTelling.Application.Abstractions;
using StoryTelling.Application.Undo;

namespace StoryTelling.Infrastructure.Diff;

public sealed class DiffPlexTextDiff : ITextDiff
{
    private readonly Differ _differ = new();

    public TextPatch CreatePatch(string oldText, string newText)
    {
        if (string.Equals(oldText, newText, StringComparison.Ordinal))
        {
            return TextPatch.Empty;
        }

        var result = _differ.CreateDiffs(oldText, newText, false, false, new LineChunker());
        var hunks = new List<LineHunk>();
        var shift = 0;

        foreach (var block in result.DiffBlocks)
        {
            var oldStart = block.DeleteStartA;
            var oldLines = result.PiecesOld.Skip(oldStart).Take(block.DeleteCountA).ToArray();
            var newLines = result.PiecesNew.Skip(block.InsertStartB).Take(block.InsertCountB).ToArray();
            var newStart = oldStart + shift;

            hunks.Add(new LineHunk(oldStart, newStart, oldLines, newLines));
            shift += block.InsertCountB - block.DeleteCountA;
        }

        return new TextPatch(hunks);
    }

    private sealed class LineChunker : IChunker
    {
        public IReadOnlyList<string> Chunk(string text) => text.Split('\n');
    }
}
