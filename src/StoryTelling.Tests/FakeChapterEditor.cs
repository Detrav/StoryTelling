using StoryTelling.Application.Chapters;
using StoryTelling.Application.Generation;
using StoryTelling.Domain;

namespace StoryTelling.Tests;

internal sealed class FakeChapterEditor : IChapterEditor
{
    public string Result { get; set; } = "Edited text.";

    public List<EditorNote> Notes { get; set; } = [];

    public string? LastDraft { get; private set; }

    public Task<ChapterEdit> EditAsync(
        Project project,
        Chapter chapter,
        string draft,
        WorldState stateBefore,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        LastDraft = draft;
        return Task.FromResult(new ChapterEdit(Result, Notes));
    }
}
