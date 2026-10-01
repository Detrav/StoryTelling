using StoryTelling.Application.Chapters;
using StoryTelling.Application.Generation;

namespace StoryTelling.Tests;

internal sealed class FakeChapterAgent : IChapterAgent
{
    public string Text { get; set; } = "Generated chapter text.";

    public int ToolCalls { get; set; } = 1;

    public WriterContext? LastContext { get; private set; }

    public Task<ChapterDraft> WriteAsync(
        WriterContext context,
        IProgress<GenerationProgress>? progress = null,
        Func<string, Task>? onDelta = null,
        CancellationToken cancellationToken = default)
    {
        LastContext = context;
        return Task.FromResult(new ChapterDraft(Text, ToolCalls));
    }
}
