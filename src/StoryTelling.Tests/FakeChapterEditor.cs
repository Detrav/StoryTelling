using StoryTelling.Application.Chapters;
using StoryTelling.Application.Generation;
using StoryTelling.Domain;

namespace StoryTelling.Tests;

internal sealed class FakeChapterEditor : IChapterEditor
{
    public string Result { get; set; } = "Edited text.";

    public List<EditorNote> Notes { get; set; } = [];

    public EditorVerdict Verdict { get; set; } = EditorVerdict.Ok;

    public string? LastDraft { get; private set; }

    public List<EditorStage> Stages { get; } = [];

    public Task<ChapterEdit> EditAsync(
        Project project,
        Chapter chapter,
        string draft,
        WorldState stateBefore,
        EditorStage stage,
        IReadOnlyList<EditorIssue>? knownIssues = null,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        LastDraft = draft;
        Stages.Add(stage);
        return Task.FromResult(new ChapterEdit(Result, Notes, Verdict));
    }
}
