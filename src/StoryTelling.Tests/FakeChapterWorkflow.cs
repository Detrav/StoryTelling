using StoryTelling.Application.Chapters;
using StoryTelling.Application.Generation;
using StoryTelling.Application.Review;
using StoryTelling.Domain;

namespace StoryTelling.Tests;

internal sealed class FakeChapterWorkflow : IChapterWorkflow
{
    public Queue<ChapterResult> Results { get; } = new();

    public List<IReadOnlyList<EditorIssue>> KnownIssues { get; } = [];

    public int CallCount { get; private set; }

    public Task<ChapterResult> RunAsync(
        Project project,
        Chapter chapter,
        WorldState stateBefore,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default) =>
        RunAsync(project, chapter, stateBefore, [], progress, cancellationToken);

    public Task<ChapterResult> RunAsync(
        Project project,
        Chapter chapter,
        WorldState stateBefore,
        IReadOnlyList<EditorIssue> knownIssues,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        KnownIssues.Add(knownIssues);
        return Task.FromResult(Results.Count > 0 ? Results.Dequeue() : Result("text", EditorVerdict.Ok, []));
    }

    public static ChapterResult Result(string text, EditorVerdict verdict, IReadOnlyList<KnowledgeChange> changes) =>
        new(text, "Log.", new WorldState { TimeAndPlace = "Here" }, changes, [], [], [], 0)
        {
            Verdict = verdict,
        };

    public static EditorVerdict Error(string detail) =>
        new(false, [new EditorIssue(ReviewSeverity.Error, detail, "X")]);
}
