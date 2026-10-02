using StoryTelling.Application.Generation;
using StoryTelling.Application.Llm;

namespace StoryTelling.Application.Chapters;

public interface IChapterAgent
{
    Task<ChapterDraft> WriteAsync(
        WriterContext context,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
