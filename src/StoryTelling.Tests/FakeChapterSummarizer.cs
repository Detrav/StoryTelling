using StoryTelling.Application.Chapters;
using StoryTelling.Application.Generation;
using StoryTelling.Domain;

namespace StoryTelling.Tests;

internal sealed class FakeChapterSummarizer : IChapterSummarizer
{
    public ChapterSummary Summary { get; set; } = new("Logline.", new WorldState(), [], [], []);

    public Func<Chapter, ChapterSummary>? SummaryFactory { get; set; }

    public Chapter? LastChapter { get; private set; }

    public IReadOnlyList<KnowledgeEntry>? LastKnowledge { get; private set; }

    public int CallCount { get; private set; }

    public Task<ChapterSummary> SummarizeAsync(
        Chapter chapter,
        WorldState stateBefore,
        IReadOnlyList<KnowledgeEntry> knowledge,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        LastChapter = chapter;
        LastKnowledge = knowledge;
        CallCount++;
        return Task.FromResult(SummaryFactory?.Invoke(chapter) ?? Summary);
    }
}



