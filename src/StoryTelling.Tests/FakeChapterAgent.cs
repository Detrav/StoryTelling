using StoryTelling.Application.Chapters;
using StoryTelling.Application.Generation;

namespace StoryTelling.Tests;

internal sealed class FakeChapterAgent : IChapterAgent
{
    public string Text { get; set; } = "Generated chapter text.";

    public int ToolCalls { get; set; } = 1;

    public WriterContext? LastContext { get; private set; }

    public IReadOnlyList<EditorIssue>? LastKnownIssues { get; private set; }

    public int CallCount { get; private set; }

    public Task<ChapterDraft> WriteAsync(
        WriterContext context,
        IReadOnlyList<EditorIssue>? knownIssues = null,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        LastContext = context;
        LastKnownIssues = knownIssues;
        CallCount++;
        return Task.FromResult(new ChapterDraft(Text, ToolCalls));
    }
}
