using StoryTelling.Application.Abstractions;
using StoryTelling.Application.Generation;
using StoryTelling.Domain;

namespace StoryTelling.Application.Chapters;

public sealed class ChapterWorkflow : IChapterWorkflow
{
    private readonly IChapterAgent _writer;
    private readonly IChapterEditor _editor;
    private readonly IChapterSummarizer _summarizer;
    private readonly ISettingsService _settingsService;

    public ChapterWorkflow(
        IChapterAgent writer,
        IChapterEditor editor,
        IChapterSummarizer summarizer,
        ISettingsService settingsService)
    {
        _writer = writer;
        _editor = editor;
        _summarizer = summarizer;
        _settingsService = settingsService;
    }

    public async Task<ChapterResult> RunAsync(
        Project project,
        Chapter chapter,
        WorldState stateBefore,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.LoadAsync(cancellationToken).ConfigureAwait(false);
        var writerContext = new WriterContext(
            project,
            chapter,
            stateBefore,
            settings.ContextTokenBudget,
            settings.RecentLoglineCount,
            settings.ContextRequiredSectionMaxChars);
        var draft = await _writer.WriteAsync(writerContext, progress, cancellationToken).ConfigureAwait(false);

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
