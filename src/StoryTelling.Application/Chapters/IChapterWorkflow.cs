using StoryTelling.Application.Generation;
using StoryTelling.Domain;

namespace StoryTelling.Application.Chapters;

public interface IChapterWorkflow
{
    Task<ChapterResult> RunAsync(
        Project project,
        Chapter chapter,
        WorldState stateBefore,
        IProgress<GenerationProgress>? progress = null,
        Func<string, Task>? onDelta = null,
        CancellationToken cancellationToken = default);
}
