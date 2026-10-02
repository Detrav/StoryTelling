using StoryTelling.Application.Abstractions;
using StoryTelling.Application.Generation;
using StoryTelling.Application.Knowledge;
using StoryTelling.Application.Prompts;
using StoryTelling.Domain;

namespace StoryTelling.Application.Chapters;

public sealed class ChapterRunner : IChapterRunner
{
    private readonly IChapterWorkflow _workflow;
    private readonly IChapterSummarizer _summarizer;
    private readonly IClock _clock;

    public ChapterRunner(IChapterWorkflow workflow, IChapterSummarizer summarizer, IClock clock)
    {
        _workflow = workflow;
        _summarizer = summarizer;
        _clock = clock;
    }

    public async Task<ChapterResult> GenerateAsync(
        Project project,
        Chapter chapter,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var stateBefore = StateBefore(project, project.Chapters.IndexOf(chapter));
        var effective = WithKnowledge(project, KnowledgeComposer.Compose(project, chapter.Number));
        var result = await _workflow.RunAsync(effective, ForWriting(project, chapter), stateBefore, progress, cancellationToken).ConfigureAwait(false);

        if (IsFinale(project, chapter) && FinaleGuard.IsUnresolved(result))
        {
            result = await _workflow.RunAsync(effective, ForWriting(project, chapter, strict: true), stateBefore, progress, cancellationToken).ConfigureAwait(false);
        }

        Apply(chapter, result);
        MarkLaterStale(project, chapter.Number);
        return result;
    }

    public async Task<ChapterResult> GenerateNextAsync(
        Project project,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var number = project.Chapters.Count + 1;
        var stateBefore = project.Chapters
            .OrderByDescending(chapter => chapter.Number)
            .Select(chapter => chapter.WorldState)
            .FirstOrDefault(state => state is not null)
            ?? project.InitialWorldState;

        var chapter = new Chapter
        {
            Number = number,
            Title = $"Chapter {number}",
            Status = ChapterStatus.Draft,
            CreatedUtc = _clock.UtcNow,
        };
        project.Chapters.Add(chapter);

        try
        {
            var effective = WithKnowledge(project, KnowledgeComposer.Compose(project, number));
            var result = await _workflow.RunAsync(effective, ForWriting(project, chapter), stateBefore, progress, cancellationToken).ConfigureAwait(false);

            if (IsFinale(project, chapter) && FinaleGuard.IsUnresolved(result))
            {
                result = await _workflow.RunAsync(effective, ForWriting(project, chapter, strict: true), stateBefore, progress, cancellationToken).ConfigureAwait(false);
            }

            Apply(chapter, result);
            return result;
        }
        catch
        {
            project.Chapters.Remove(chapter);
            throw;
        }
    }

    public async Task<IReadOnlyList<ChapterResult>> RunAsync(
        Project project,
        int count,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<ChapterResult>();
        for (var i = 0; i < count; i++)
        {
            results.Add(await GenerateNextAsync(project, progress, cancellationToken).ConfigureAwait(false));
        }

        return results;
    }

    public async Task<ChapterSummary> RegenerateSummaryAsync(
        Project project,
        Chapter chapter,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(chapter.ContentOriginal))
        {
            throw new InvalidOperationException("The chapter has no text to summarize.");
        }

        var stateBefore = StateBefore(project, project.Chapters.IndexOf(chapter));
        var knowledge = KnowledgeComposer.Compose(project, chapter.Number);
        var finished = new Chapter { Number = chapter.Number, Title = chapter.Title, ContentOriginal = chapter.ContentOriginal };
        var summary = await _summarizer
            .SummarizeAsync(finished, stateBefore, knowledge, progress, cancellationToken)
            .ConfigureAwait(false);

        chapter.Logline = summary.Logline;
        chapter.WorldState = summary.WorldState;
        chapter.KnowledgeChanges = [.. summary.KnowledgeChanges];
        if (chapter.Status == ChapterStatus.Stale)
        {
            chapter.Status = ChapterStatus.Generated;
        }

        MarkLaterStale(project, chapter.Number);
        return summary;
    }

    public async Task RecomputeFromAsync(
        Project project,
        int number,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var chapters = project.Chapters
            .Where(chapter => chapter.Number >= number && !string.IsNullOrWhiteSpace(chapter.ContentOriginal))
            .OrderBy(chapter => chapter.Number)
            .ToList();

        foreach (var chapter in chapters)
        {
            var stateBefore = StateBefore(project, project.Chapters.IndexOf(chapter));
            var finished = new Chapter { Number = chapter.Number, Title = chapter.Title, ContentOriginal = chapter.ContentOriginal };
            var knowledge = KnowledgeComposer.Compose(project, chapter.Number);
            var summary = await _summarizer
                .SummarizeAsync(finished, stateBefore, knowledge, progress, cancellationToken)
                .ConfigureAwait(false);

            chapter.Logline = summary.Logline;
            chapter.WorldState = summary.WorldState;
            chapter.KnowledgeChanges = [.. summary.KnowledgeChanges];
            if (chapter.Status == ChapterStatus.Stale)
            {
                chapter.Status = ChapterStatus.Generated;
            }
        }
    }

    private static bool IsFinale(Project project, Chapter chapter) =>
        chapter.Role == ChapterRole.Finale
        || (project.Chapters.Count > 0 && chapter.Number == project.Chapters.Max(candidate => candidate.Number));

    private static Chapter ForWriting(Project project, Chapter chapter, bool strict = false)
    {
        if (!IsFinale(project, chapter))
        {
            return chapter;
        }

        var notes = string.IsNullOrWhiteSpace(chapter.Notes)
            ? FinaleGuard.Contract
            : chapter.Notes.TrimEnd() + "\n" + FinaleGuard.Contract;
        if (strict)
        {
            notes += "\n" + FinaleGuard.StrictContract;
        }

        return CopyWithNotes(chapter, notes);
    }

    private static Chapter CopyWithNotes(Chapter chapter, string notes) => new()
    {
        Number = chapter.Number,
        Title = chapter.Title,
        Role = chapter.Role,
        Direction = chapter.Direction,
        Notes = notes,
        ContentOriginal = chapter.ContentOriginal,
        Translations = new(chapter.Translations),
        TranslatedTitles = new(chapter.TranslatedTitles),
        StaleTranslations = [.. chapter.StaleTranslations],
        Logline = chapter.Logline,
        WorldState = chapter.WorldState,
        KnowledgeChanges = [.. chapter.KnowledgeChanges],
        EditorNotes = [.. chapter.EditorNotes],
        Status = chapter.Status,
        CreatedUtc = chapter.CreatedUtc,
    };

    private static void Apply(Chapter chapter, ChapterResult result)
    {
        chapter.ContentOriginal = result.Text;
        chapter.Logline = result.Logline;
        chapter.WorldState = result.WorldState;
        chapter.KnowledgeChanges = [.. result.KnowledgeChanges];
        chapter.EditorNotes = [.. result.EditorNotes];
        MarkTranslationsStale(chapter);
        chapter.Status = ChapterStatus.Generated;
    }

    private static void MarkTranslationsStale(Chapter chapter)
    {
        foreach (var code in chapter.Translations.Keys)
        {
            if (!chapter.StaleTranslations.Contains(code))
            {
                chapter.StaleTranslations.Add(code);
            }
        }
    }

    private static Project WithKnowledge(Project project, IReadOnlyList<KnowledgeEntry> knowledge) => new()
    {
        SchemaVersion = project.SchemaVersion,
        Id = project.Id,
        Name = project.Name,
        CreatedUtc = project.CreatedUtc,
        UpdatedUtc = project.UpdatedUtc,
        Settings = project.Settings,
        World = project.World,
        Knowledge = [.. knowledge],
        Chapters = project.Chapters,
        InitialWorldState = project.InitialWorldState,
    };

    private static WorldState StateBefore(Project project, int index) =>
        index > 0 ? project.Chapters[index - 1].WorldState ?? project.InitialWorldState : project.InitialWorldState;

    private static void MarkLaterStale(Project project, int number)
    {
        foreach (var chapter in project.Chapters)
        {
            if (chapter.Number > number && chapter.Status != ChapterStatus.Draft)
            {
                chapter.Status = ChapterStatus.Stale;
            }
        }
    }
}
