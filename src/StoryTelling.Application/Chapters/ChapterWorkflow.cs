using StoryTelling.Application.Generation;
using StoryTelling.Domain;

namespace StoryTelling.Application.Chapters;

public sealed class ChapterWorkflow : IChapterWorkflow
{
    private readonly IChapterAgent _writer;
    private readonly IChapterEditor _editor;
    private readonly IChapterSummarizer _summarizer;

    public ChapterWorkflow(IChapterAgent writer, IChapterEditor editor, IChapterSummarizer summarizer)
    {
        _writer = writer;
        _editor = editor;
        _summarizer = summarizer;
    }

    public async Task<ChapterResult> RunAsync(
        Project project,
        Chapter chapter,
        WorldState stateBefore,
        IProgress<GenerationProgress>? progress = null,
        Func<string, Task>? onDelta = null,
        CancellationToken cancellationToken = default)
    {
        var writerContext = new WriterContext(project, chapter, stateBefore, ChapterContextAssembler.DefaultTokenBudget);
        var draft = await _writer.WriteAsync(writerContext, progress, onDelta, cancellationToken).ConfigureAwait(false);

        var edit = await _editor
            .EditAsync(project, chapter, draft.Text, stateBefore, progress, cancellationToken)
            .ConfigureAwait(false);

        var finished = new Chapter { Number = chapter.Number, Title = chapter.Title, ContentOriginal = edit.Text };
        var summary = await _summarizer
            .SummarizeAsync(finished, stateBefore, project.Knowledge, progress, cancellationToken)
            .ConfigureAwait(false);

        return new ChapterResult(edit.Text, summary.Logline, summary.WorldState, summary.KnowledgeChanges, edit.Notes, draft.ToolCalls);
    }
}
