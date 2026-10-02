using StoryTelling.Application.Chapters;
using StoryTelling.Application.Generation;
using StoryTelling.Domain;

namespace StoryTelling.Tests;

internal sealed class FakeChapterRunner : IChapterRunner
{
    public string Text { get; set; } = "Generated chapter text.";

    public string Logline { get; set; } = "A logline.";

    public WorldState WorldState { get; set; } = new() { TimeAndPlace = "Somewhere" };

    public int ToolCalls { get; set; } = 1;

    public List<KnowledgeChange> KnowledgeChanges { get; set; } = [];

    public List<EditorNote> EditorNotes { get; set; } = [];

    public WorldState? LastStateBefore { get; private set; }

    public Exception? Throws { get; set; }

    public Task<ChapterResult> GenerateAsync(
        Project project,
        Chapter chapter,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (Throws is not null)
        {
            return Task.FromException<ChapterResult>(Throws);
        }

        var index = project.Chapters.IndexOf(chapter);
        LastStateBefore = index > 0 ? project.Chapters[index - 1].WorldState ?? project.InitialWorldState : project.InitialWorldState;
        return Task.FromResult(new ChapterResult(Text, Logline, WorldState, KnowledgeChanges, EditorNotes, ToolCalls));
    }

    public Task<ChapterResult> GenerateNextAsync(
        Project project,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<IReadOnlyList<ChapterResult>> RunAsync(
        Project project,
        int count,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task RecomputeFromAsync(
        Project project,
        int number,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public ChapterSummary Summary { get; set; } = new("Regenerated logline.", new WorldState { TimeAndPlace = "Elsewhere" }, []);

    public Task<ChapterSummary> RegenerateSummaryAsync(
        Project project,
        Chapter chapter,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        chapter.Logline = Summary.Logline;
        chapter.WorldState = Summary.WorldState;
        chapter.KnowledgeChanges = [.. Summary.KnowledgeChanges];
        return Task.FromResult(Summary);
    }
}
