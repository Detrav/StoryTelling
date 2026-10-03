using StoryTelling.Application.Abstractions;
using StoryTelling.Application.Generation;
using StoryTelling.Application.Settings;
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
        var text = draft.Text;

        var checks = SelectChecks(settings);
        var verdict = EditorChecklistVerdict.Empty;
        var notes = new List<EditorNote>();

        if (checks.Count > 0)
        {
            progress?.Report(new GenerationProgress("Checking consistency", 0));
            verdict = await _editor.CheckAsync(writerContext, text, checks, progress, cancellationToken).ConfigureAwait(false);

            foreach (var failure in verdict.Failures)
            {
                if (EditorChecks.Find(failure.Id) is not { } check)
                {
                    continue;
                }

                progress?.Report(new GenerationProgress($"Fixing {check.Label}", 0));
                text = await _editor.FixAsync(writerContext, text, check, failure.Reason, progress, cancellationToken).ConfigureAwait(false);
            }
        }

        if (settings.CosmeticEditorEnabled)
        {
            progress?.Report(new GenerationProgress("Polishing", 0));
            var cosmetic = await _editor.CosmeticAsync(writerContext, text, progress, cancellationToken).ConfigureAwait(false);
            text = cosmetic.Text;
            notes.AddRange(cosmetic.Notes);
        }

        var finished = new Chapter { Number = chapter.Number, Title = chapter.Title, ContentOriginal = text };
        var summary = await _summarizer
            .SummarizeAsync(finished, stateBefore, project.Knowledge, progress, cancellationToken)
            .ConfigureAwait(false);

        return new ChapterResult(text, summary.Logline, summary.WorldState, summary.KnowledgeChanges, notes, summary.ContinuityIssues, summary.DirectionRewrites, draft.ToolCalls)
        {
            Checklist = verdict,
        };
    }

    private static IReadOnlyList<EditorCheck> SelectChecks(AppSettings settings)
    {
        if (settings.EnabledEditorChecks is { Count: > 0 } ids)
        {
            return [.. ids.Select(EditorChecks.Find).Where(check => check is not null).Select(check => check!)];
        }

        return EditorChecks.All;
    }
}
