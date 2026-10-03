using StoryTelling.Application.Chapters;
using StoryTelling.Application.Generation;
using StoryTelling.Domain;

namespace StoryTelling.Tests;

internal sealed class FakeChapterWorkflow : IChapterWorkflow
{
    public Queue<ChapterResult> Results { get; } = new();

    public int CallCount { get; private set; }

    public Task<ChapterResult> RunAsync(
        Project project,
        Chapter chapter,
        WorldState stateBefore,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        return Task.FromResult(Results.Count > 0 ? Results.Dequeue() : Result("text", []));
    }

    public static ChapterResult Result(string text, IReadOnlyList<KnowledgeChange> changes) =>
        new(text, "Log.", new WorldState { TimeAndPlace = "Here" }, changes, [], [], [], 0);
}
