using StoryTelling.Application.Generation;
using StoryTelling.Domain;

namespace StoryTelling.Application.Chapters;

public interface IChapterSummarizer
{
    Task<ChapterSummary> SummarizeAsync(
        Chapter chapter,
        WorldState stateBefore,
        IReadOnlyList<KnowledgeEntry> knowledge,
        string previousStorySoFar = "",
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
