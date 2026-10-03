using StoryTelling.Application.Chapters;
using StoryTelling.Application.Generation;
using StoryTelling.Domain;

namespace StoryTelling.Tests;

internal sealed class FakeChapterEditor : IChapterEditor
{
    public EditorChecklistVerdict Verdict { get; set; } = EditorChecklistVerdict.Empty;

    public string FixResult { get; set; } = "Fixed text.";

    public string CosmeticResult { get; set; } = "Polished text.";

    public List<EditorNote> CosmeticNotes { get; set; } = [];

    public List<EditorCheck> Fixed { get; } = [];

    public List<string> FixInputs { get; } = [];

    public bool CosmeticCalled { get; private set; }

    public Task<EditorChecklistVerdict> CheckAsync(
        WriterContext context,
        string text,
        IReadOnlyList<EditorCheck> checks,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default) => Task.FromResult(Verdict);

    public Task<string> FixAsync(
        WriterContext context,
        string text,
        EditorCheck check,
        string reason,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Fixed.Add(check);
        FixInputs.Add(text);
        return Task.FromResult(FixResult);
    }

    public Task<ChapterEdit> CosmeticAsync(
        WriterContext context,
        string text,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        CosmeticCalled = true;
        return Task.FromResult(new ChapterEdit(CosmeticResult, CosmeticNotes));
    }
}
