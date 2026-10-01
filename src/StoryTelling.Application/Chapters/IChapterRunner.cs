using StoryTelling.Application.Generation;
using StoryTelling.Domain;

namespace StoryTelling.Application.Chapters;

public interface IChapterRunner
{
    Task<ChapterResult> GenerateAsync(
        Project project,
        Chapter chapter,
        IProgress<GenerationProgress>? progress = null,
        Func<string, Task>? onDelta = null,
        CancellationToken cancellationToken = default);

    Task<ChapterResult> GenerateNextAsync(
        Project project,
        IProgress<GenerationProgress>? progress = null,
        Func<string, Task>? onDelta = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChapterResult>> RunAsync(
        Project project,
        int count,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<ChapterSummary> RegenerateSummaryAsync(
        Project project,
        Chapter chapter,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task RecomputeFromAsync(
        Project project,
        int number,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
