using StoryTelling.Application.Chapters;
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

    public EditorChecklistVerdict Check(
        IReadOnlyList<EditorCheck> checks) => Verdict;

    public Task<EditorChecklistVerdict> CheckAsync(
        Project project,
        Chapter chapter,
        string text,
        WorldState stateBefore,
        IReadOnlyList<EditorCheck> checks,
        CancellationToken cancellationToken = default) => Task.FromResult(Verdict);

    public Task<string> FixAsync(
        Project project,
        Chapter chapter,
        string text,
        WorldState stateBefore,
        EditorCheck check,
        string reason,
        CancellationToken cancellationToken = default)
    {
        Fixed.Add(check);
        FixInputs.Add(text);
        return Task.FromResult(FixResult);
    }

    public Task<ChapterEdit> CosmeticAsync(
        Project project,
        Chapter chapter,
        string text,
        WorldState stateBefore,
        CancellationToken cancellationToken = default)
    {
        CosmeticCalled = true;
        return Task.FromResult(new ChapterEdit(CosmeticResult, CosmeticNotes));
    }
}
