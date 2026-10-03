using StoryTelling.Application.Generation;

namespace StoryTelling.Application.Chapters;

public interface IChapterAgent
{
    Task<ChapterDraft> WriteAsync(
        WriterContext context,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
