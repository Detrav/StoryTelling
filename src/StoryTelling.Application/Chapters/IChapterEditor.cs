using StoryTelling.Application.Generation;
using StoryTelling.Domain;

namespace StoryTelling.Application.Chapters;

public interface IChapterEditor
{
    Task<EditorChecklistVerdict> CheckAsync(
        WriterContext context,
        string text,
        IReadOnlyList<EditorCheck> checks,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<string> FixAsync(
        WriterContext context,
        string text,
        EditorCheck check,
        string reason,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<ChapterEdit> CosmeticAsync(
        WriterContext context,
        string text,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
