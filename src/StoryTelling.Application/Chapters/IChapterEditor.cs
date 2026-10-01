using StoryTelling.Application.Generation;
using StoryTelling.Domain;

namespace StoryTelling.Application.Chapters;

public interface IChapterEditor
{
    Task<ChapterEdit> EditAsync(
        Project project,
        Chapter chapter,
        string draft,
        WorldState stateBefore,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
