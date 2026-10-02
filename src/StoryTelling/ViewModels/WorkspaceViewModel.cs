using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StoryTelling.Application.Abstractions;
using StoryTelling.Application.Chapters;
using StoryTelling.Application.Generation;
using StoryTelling.Application.Knowledge;
using StoryTelling.Application.Llm;
using StoryTelling.Application.Review;
using StoryTelling.Application.Settings;
using StoryTelling.Application.Translation;
using StoryTelling.Domain;

namespace StoryTelling.ViewModels;

public partial class WorkspaceViewModel : ViewModelBase
{
    private readonly Project _project;
    private readonly IClock _clock;
    private readonly IChapterRunner _chapterRunner;
    private readonly IGenerationAssistant _assistant;
    private readonly ITranslationService _translationService;
    private readonly IMetadataTranslator _metadataTranslator;
    private readonly IContinuityReviewer? _continuityReviewer;
    private IUndoRedoHost? _undoRedo;
    private ObservableObject? _undoRedoHost;
    private bool _suppressMutation;

    public WorkspaceViewModel(Project project, IClock clock, IChapterRunner chapterRunner, IGenerationAssistant assistant, ITranslationService translationService, IMetadataTranslator metadataTranslator, IContinuityReviewer? continuityReviewer = null)
    {
        _project = project;
        _clock = clock;
        _chapterRunner = chapterRunner;
        _assistant = assistant;
        _translationService = translationService;
        _metadataTranslator = metadataTranslator;
        _continuityReviewer = continuityReviewer;
        _projectName = project.Name;

        Chapters.CollectionChanged += (_, _) => Renumber();

        foreach (var code in project.Settings.TargetLanguages)
        {
            Languages.Add(code);
        }

        foreach (var chapter in project.Chapters)
        {
            var viewModel = FromChapter(chapter);
            BuildTabs(viewModel);
            Chapters.Add(viewModel);
        }

        if (Chapters.Count == 0)
        {
            AddNewChapter();
        }

        _selectedChapter = Chapters[0];
    }

    public event Action? CloseRequested;

    public event Action<string>? Mutated;

    public event Action<string, string>? WarningRequested;

    public void AttachUndoRedo(IUndoRedoHost host)
    {
        DetachUndoRedo();
        _undoRedo = host;

        if (host is ObservableObject observable)
        {
            _undoRedoHost = observable;
            observable.PropertyChanged += OnUndoRedoHostChanged;
        }

        OnPropertyChanged(nameof(HasUndoRedo));
        OnPropertyChanged(nameof(UndoCommand));
        OnPropertyChanged(nameof(RedoCommand));
        OnPropertyChanged(nameof(UndoLabel));
        OnPropertyChanged(nameof(RedoLabel));
    }

    public void DetachUndoRedo()
    {
        if (_undoRedoHost is not null)
        {
            _undoRedoHost.PropertyChanged -= OnUndoRedoHostChanged;
            _undoRedoHost = null;
        }

        _undoRedo = null;
        OnPropertyChanged(nameof(HasUndoRedo));
        OnPropertyChanged(nameof(UndoCommand));
        OnPropertyChanged(nameof(RedoCommand));
        OnPropertyChanged(nameof(UndoLabel));
        OnPropertyChanged(nameof(RedoLabel));
    }

    public bool HasUndoRedo => _undoRedo is not null;

    public IAsyncRelayCommand? UndoCommand => _undoRedo?.UndoCommand;

    public IAsyncRelayCommand? RedoCommand => _undoRedo?.RedoCommand;

    public string UndoLabel => _undoRedo?.UndoLabel ?? "Undo";

    public string RedoLabel => _undoRedo?.RedoLabel ?? "Redo";

    private void OnUndoRedoHostChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(IUndoRedoHost.UndoLabel):
            case nameof(IUndoRedoHost.RedoLabel):
                OnPropertyChanged(e.PropertyName);
                break;
            default:
                _undoRedo?.UndoCommand.NotifyCanExecuteChanged();
                _undoRedo?.RedoCommand.NotifyCanExecuteChanged();
                break;
        }
    }

    [ObservableProperty]
    private string _projectName;

    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    private bool _isSidebarVisible = true;

    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private ChapterViewModel _selectedChapter;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _status = string.Empty;

    public ObservableCollection<string> Languages { get; } = [];

    public ObservableCollection<ChapterViewModel> Chapters { get; } = [];

    public string? FilePath { get; set; }

    [RelayCommand]
    private void AddChapter()
    {
        SelectedChapter = AddNewChapter();
        MarkMetadataStale();
        IsDirty = true;
        Mutated?.Invoke("Add chapter");
    }

    [RelayCommand]
    private void DeleteChapter(ChapterViewModel? chapter)
    {
        if (Chapters.Count <= 1)
        {
            return;
        }

        var target = chapter ?? SelectedChapter;
        var index = Chapters.IndexOf(target);
        if (index < 0)
        {
            return;
        }

        Chapters.RemoveAt(index);
        Renumber();
        SelectedChapter = Chapters[Math.Min(index, Chapters.Count - 1)];
        MarkMetadataStale();
        IsDirty = true;
        Mutated?.Invoke("Delete chapter");
    }

    [RelayCommand]
    private void MoveChapterUp(ChapterViewModel? chapter)
    {
        var target = chapter ?? SelectedChapter;
        var index = Chapters.IndexOf(target);
        if (index <= 0)
        {
            return;
        }

        Chapters.Move(index, index - 1);
        Renumber();
        IsDirty = true;
        Mutated?.Invoke("Move chapter up");
    }

    [RelayCommand]
    private void MoveChapterDown(ChapterViewModel? chapter)
    {
        var target = chapter ?? SelectedChapter;
        var index = Chapters.IndexOf(target);
        if (index < 0 || index >= Chapters.Count - 1)
        {
            return;
        }

        Chapters.Move(index, index + 1);
        Renumber();
        IsDirty = true;
        Mutated?.Invoke("Move chapter down");
    }

    public void SetChapterStatus(ChapterViewModel? chapter, ChapterStatus status)
    {
        if (chapter is null || chapter.Status == status)
        {
            return;
        }

        chapter.Status = status;
        IsDirty = true;
        Mutated?.Invoke($"Set status of chapter {chapter.Number}");
    }

    public async Task PlanFinalChapterAsync(
        IProgress<ProgressTaskProgress> progress,
        CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            throw new InvalidOperationException("Another operation is in progress. Wait for it to finish.");
        }

        AddChapter();
        var chapter = SelectedChapter;

        IsBusy = true;
        Status = "Planning the final chapter…";
        progress.Report(new ProgressTaskProgress(0, 1, 0, "Planning the final chapter…"));

        try
        {
            var project = ToProject();
            var priorChapters = project.Chapters.Where(candidate => candidate.Number < chapter.Number).ToList();
            var effective = WithKnowledge(project, KnowledgeComposer.Compose(project, chapter.Number), priorChapters);
            var request = new GenerationRequest
            {
                Target = GenerationTarget.Finale,
                Variants = 1,
                Context = new GenerationContext { Fields = ProjectFields() },
                Snapshot = effective,
                Avoid =
                [
                    .. project.Chapters
                        .Where(candidate => candidate.Number != chapter.Number)
                        .Select(candidate => candidate.Title.Trim())
                        .Where(title => title.Length > 0),
                ],
            };

            var inner = new Progress<GenerationProgress>(report =>
                progress.Report(new ProgressTaskProgress(0, 1, 0, DescribeStage(report))));

            var options = await _assistant.GenerateAsync(request, new GenerationSession(), inner, cancellationToken);
            var option = options.FirstOrDefault()
                ?? throw new InvalidOperationException("The model returned no usable plan. Try again.");

            if (option.Fields.TryGetValue("Title", out var title) && !string.IsNullOrWhiteSpace(title))
            {
                chapter.Title = title.Trim();
            }

            if (option.Fields.TryGetValue("Direction", out var direction))
            {
                chapter.Direction = direction.Trim();
            }

            Status = "Final chapter planned — review the direction, then Generate.";
            Mutated?.Invoke("Plan final chapter");
            progress.Report(new ProgressTaskProgress(1, 1, -1, string.Empty));
        }
        catch (OperationCanceledException)
        {
            Status = "Stopped.";
            throw;
        }
        catch (Exception exception)
        {
            Status = $"Failed: {exception.Message}";
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public IReadOnlyList<BookOperation> BuildCompletionPlan()
    {
        var operations = new List<BookOperation>();
        var cascade = false;

        foreach (var chapter in Chapters)
        {
            if (cascade || NeedsChapterWork(chapter))
            {
                var missing = MissingForGeneration(chapter);
                if (missing.Count > 0)
                {
                    operations.Add(new BookOperation(
                        BookOperationKind.Skip,
                        chapter.Number,
                        $"Chapter {chapter.Number} — skipped",
                        SkipReason: string.Join("; ", missing)));
                    continue;
                }

                operations.Add(new BookOperation(
                    BookOperationKind.WriteChapter,
                    chapter.Number,
                    $"Write {DescribeChapter(chapter)}"));
                cascade = true;

                foreach (var translation in chapter.Translations)
                {
                    operations.Add(TranslateOperation(chapter, translation.LanguageCode));
                }

                continue;
            }

            if (NeedsSummary(chapter))
            {
                operations.Add(new BookOperation(
                    BookOperationKind.SummarizeChapter,
                    chapter.Number,
                    $"Summarize {DescribeChapter(chapter)}"));
                cascade = true;
            }

            foreach (var translation in chapter.Translations)
            {
                if (NeedsTranslation(chapter, translation))
                {
                    operations.Add(TranslateOperation(chapter, translation.LanguageCode));
                }
            }
        }

        return operations;
    }

    public async Task CompleteBookAsync(
        IReadOnlyList<BookOperation> operations,
        IProgress<BookCompletionProgress> progress,
        CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            throw new InvalidOperationException("Another operation is in progress. Wait for it to finish.");
        }

        IsBusy = true;
        _suppressMutation = true;

        var completed = 0;
        var applied = 0;
        void Report(int currentIndex, string stage) =>
            progress.Report(new BookCompletionProgress(completed, operations.Count, currentIndex, stage));

        try
        {
            for (var index = 0; index < operations.Count; index++)
            {
                var operation = operations[index];
                cancellationToken.ThrowIfCancellationRequested();

                var chapter = Chapters.FirstOrDefault(candidate => candidate.Number == operation.ChapterNumber);
                if (chapter is null || operation.Kind == BookOperationKind.Skip)
                {
                    completed++;
                    Report(-1, string.Empty);
                    continue;
                }

                if (operation.Kind == BookOperationKind.WriteChapter)
                {
                    var writeProgress = new Progress<GenerationProgress>(report =>
                        Report(index, DescribeStage(report)));
                    Report(index, "Writing…");
                    await WriteChapterCoreAsync(chapter, writeProgress, cancellationToken);
                    applied++;
                }
                else if (operation.Kind == BookOperationKind.SummarizeChapter)
                {
                    var summaryProgress = new Progress<GenerationProgress>(report =>
                        Report(index, DescribeStage(report)));
                    Report(index, "Summarizing…");
                    await RegenerateSummaryAsync(chapter, summaryProgress, cancellationToken);
                    applied++;
                }
                else if (operation.Kind == BookOperationKind.TranslateChapter && operation.LanguageCode is { Length: > 0 } code)
                {
                    var translationProgress = new Progress<GenerationProgress>(report =>
                        Report(index, DescribeStage(report)));
                    Report(index, "Translating…");
                    var translated = await TranslateLanguageAsync(chapter, code, translationProgress, cancellationToken);
                    if (!string.IsNullOrEmpty(translated))
                    {
                        applied++;
                    }
                }

                completed++;
                Report(-1, string.Empty);
            }

            Status = operations.Count == 0 ? "Everything is already up to date." : "Complete book finished.";
            progress.Report(new BookCompletionProgress(completed, operations.Count, -1, string.Empty));
        }
        finally
        {
            _suppressMutation = false;
            IsBusy = false;
            if (applied > 0)
            {
                IsDirty = true;
                Mutated?.Invoke("Complete book");
            }
        }
    }

    public IReadOnlyList<string> MetadataLanguages =>
        [.. Languages.Where(code => !string.Equals(code, "en", StringComparison.OrdinalIgnoreCase))];

    public bool NeedsMetadataTranslation(string code) =>
        !MetadataTranslationCoverage.Evaluate(ToProject(), code).IsComplete;

    public IReadOnlyList<MetadataChapterTitle> ChapterTitles() =>
        [.. Chapters
            .Where(chapter => !string.IsNullOrWhiteSpace(chapter.Title))
            .OrderBy(chapter => chapter.Number)
            .Select(chapter => new MetadataChapterTitle(chapter.Number, chapter.Title.Trim()))];

    public async Task TranslateMetadataAsync(
        IProgress<MetadataTranslationProgress> progress,
        CancellationToken cancellationToken)
    {
        var languages = MetadataLanguages.ToList();
        if (IsBusy)
        {
            throw new InvalidOperationException("Another operation is in progress. Wait for it to finish.");
        }

        IsBusy = true;

        var completed = 0;
        var applied = 0;
        void Report(int currentIndex, string stage) =>
            progress.Report(new MetadataTranslationProgress(completed, languages.Count, currentIndex, stage));

        try
        {
            for (var index = 0; index < languages.Count; index++)
            {
                var code = languages[index];
                cancellationToken.ThrowIfCancellationRequested();

                if (MetadataTranslationCoverage.Evaluate(ToProject(), code).IsComplete)
                {
                    completed++;
                    Report(-1, string.Empty);
                    continue;
                }

                var position = index;
                var translationProgress = new Progress<GenerationProgress>(report =>
                    Report(position, DescribeStage(report)));
                Report(index, "Translating…");

                var project = ToProject();
                var request = new MetadataTranslationRequest(code, project.Name, project.World.Body, ChapterTitles());
                var result = await _metadataTranslator.TranslateAsync(request, translationProgress, cancellationToken);
                ApplyMetadataTranslation(code, result);
                applied++;

                completed++;
                Report(-1, string.Empty);
            }

            Status = languages.Count == 0 ? "No target languages to translate." : "Book metadata translated.";
        }
        finally
        {
            IsBusy = false;
            if (applied > 0)
            {
                IsDirty = true;
                Mutated?.Invoke("Translate book metadata");
            }
        }
    }

    private void ApplyMetadataTranslation(string code, MetadataTranslationResult result)
    {
        if (!_project.MetadataTranslations.TryGetValue(code, out var cached))
        {
            cached = new MetadataTranslation();
            _project.MetadataTranslations[code] = cached;
        }

        if (!string.IsNullOrWhiteSpace(result.BookName))
        {
            cached.Name = result.BookName;
        }

        if (!string.IsNullOrWhiteSpace(result.Annotation))
        {
            cached.Annotation = result.Annotation;
        }

        var incomplete = false;
        foreach (var chapter in Chapters)
        {
            if (result.ChapterTitles.TryGetValue(chapter.Number, out var title) && !string.IsNullOrWhiteSpace(title))
            {
                chapter.TranslatedTitles[code] = title;
            }
            else if (!string.IsNullOrWhiteSpace(chapter.Title))
            {
                incomplete = true;
            }
        }

        if (incomplete)
        {
            return;
        }

        _project.StaleMetadataTranslations.RemoveAll(stale => string.Equals(stale, code, StringComparison.OrdinalIgnoreCase));
    }

    private static string DescribeChapter(ChapterViewModel chapter) =>
        string.IsNullOrWhiteSpace(chapter.Title)
            ? $"chapter {chapter.Number}"
            : $"chapter {chapter.Number} — {chapter.Title}";

    private static BookOperation TranslateOperation(ChapterViewModel chapter, string languageCode) =>
        new(
            BookOperationKind.TranslateChapter,
            chapter.Number,
            $"Translate chapter {chapter.Number} ({languageCode.ToUpperInvariant()})",
            languageCode);

    private static bool NeedsChapterWork(ChapterViewModel chapter) =>
        string.IsNullOrWhiteSpace(chapter.ContentOriginal) || chapter.Status == ChapterStatus.Stale;

    private static bool NeedsSummary(ChapterViewModel chapter) =>
        !string.IsNullOrWhiteSpace(chapter.ContentOriginal)
        && (string.IsNullOrWhiteSpace(chapter.Logline) || chapter.WorldState is null);

    private static bool NeedsTranslation(ChapterViewModel chapter, TranslationViewModel translation) =>
        !string.IsNullOrWhiteSpace(chapter.ContentOriginal)
        && (string.IsNullOrWhiteSpace(translation.Text) || chapter.StaleTranslations.Contains(translation.LanguageCode));

    public bool ValidateGeneration()
    {
        if (SelectedChapter is not { } chapter)
        {
            return false;
        }

        var missing = MissingForGeneration(chapter);
        if (missing.Count == 0)
        {
            return true;
        }

        WarningRequested?.Invoke(
            $"Cannot generate chapter {chapter.Number}",
            "Fill in the following first:\n• " + string.Join("\n• ", missing));
        return false;
    }

    public async Task GenerateChapterAsync(
        IProgress<GenerationProgress> progress,
        CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            throw new InvalidOperationException("Another operation is in progress. Wait for it to finish.");
        }

        if (SelectedChapter is not { } chapter)
        {
            return;
        }

        IsBusy = true;
        Status = "Writing…";

        try
        {
            var result = await WriteChapterCoreAsync(chapter, progress, cancellationToken);
            Status = $"Chapter {chapter.Number} generated ({result.ToolCalls} tool calls).";
            Mutated?.Invoke($"Generate chapter {chapter.Number}");
        }
        catch (LlmException exception)
        {
            Status = $"Failed ({exception.Kind}): {exception.Message}";
            throw;
        }
        catch (OperationCanceledException)
        {
            Status = "Stopped.";
            throw;
        }
        catch (Exception exception)
        {
            Status = $"Failed: {exception.Message}";
            throw;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<ChapterResult> WriteChapterCoreAsync(
        ChapterViewModel chapter,
        IProgress<GenerationProgress>? progress,
        CancellationToken cancellationToken)
    {
        var project = ToProject();
        var target = project.Chapters.First(candidate => candidate.Number == chapter.Number);
        var editor = chapter.PrimaryTextEditor;

        var result = await _chapterRunner.GenerateAsync(project, target, progress, cancellationToken);

        editor?.ReplaceText(result.Text);

        chapter.ContentOriginal = result.Text;
        chapter.Logline = result.Logline;
        chapter.WorldState = result.WorldState;
        chapter.KnowledgeChanges = [.. result.KnowledgeChanges];
        chapter.EditorNotes = [.. result.EditorNotes];
        ApplyDirectionSuggestions(result.DirectionRewrites);
        MarkTranslationsStale(chapter);
        chapter.Status = ChapterStatus.Generated;
        MarkLaterStale(chapter.Number);
        return result;

    }

    private void ApplyDirectionSuggestions(IReadOnlyList<DirectionRewrite> rewrites)
    {
        foreach (var rewrite in rewrites)
        {
            var target = Chapters.FirstOrDefault(candidate => candidate.Number == rewrite.ChapterNumber);
            if (target is null || string.Equals(target.Direction, rewrite.Direction, StringComparison.Ordinal))
            {
                continue;
            }

            if (!target.DirectionSuggestions.Contains(rewrite.Direction))
            {
                target.DirectionSuggestions = [.. target.DirectionSuggestions, rewrite.Direction];
            }
        }
    }

    private static string DescribeStage(GenerationProgress report) =>
        report.ToolCalls > 0 ? $"{report.Stage} — {report.ToolCalls} tool calls" : report.Stage;

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke();

    private IReadOnlyList<string> MissingForGeneration(ChapterViewModel chapter)
    {
        var missing = new List<string>();
        var world = _project.World;

        if (string.IsNullOrWhiteSpace(world.Title) && string.IsNullOrWhiteSpace(world.Body))
        {
            missing.Add("the world — title and description (Project setup → World)");
        }

        var frameEmpty = string.IsNullOrWhiteSpace(world.Genre)
            && string.IsNullOrWhiteSpace(world.Tone)
            && string.IsNullOrWhiteSpace(world.Style)
            && string.IsNullOrWhiteSpace(world.PointOfView)
            && string.IsNullOrWhiteSpace(world.Tense)
            && string.IsNullOrWhiteSpace(world.Rating);
        if (frameEmpty)
        {
            missing.Add("the story frame — genre, tone, style, point of view, tense, rating (Project setup → World)");
        }

        if (StateBeforeIsEmpty(chapter))
        {
            missing.Add("the starting situation — initial world state (Project setup → Initial world state)");
        }

        if (string.IsNullOrWhiteSpace(chapter.Direction))
        {
            missing.Add($"the direction of chapter {chapter.Number} (chapter Settings tab)");
        }

        return missing;
    }

    private bool StateBeforeIsEmpty(ChapterViewModel chapter)
    {
        var prior = Chapters
            .Where(candidate => candidate.Number < chapter.Number && candidate.WorldState is not null)
            .OrderByDescending(candidate => candidate.Number)
            .FirstOrDefault()?.WorldState;

        var state = prior ?? _project.InitialWorldState;
        return string.IsNullOrWhiteSpace(state.TimeAndPlace) && string.IsNullOrWhiteSpace(state.Situation);
    }

    private void MarkLaterStale(int number)
    {
        foreach (var other in Chapters)
        {
            if (other.Number > number && other.Status != ChapterStatus.Draft)
            {
                other.Status = ChapterStatus.Stale;
            }
        }
    }

    public bool PrepareTranslation()
    {
        if (SelectedChapter is not { } chapter || chapter.Translations.Count == 0)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(chapter.ContentOriginal))
        {
            WarningRequested?.Invoke(
                "Nothing to translate",
                "This chapter has no text yet. Generate or write it before translating.");
            return false;
        }

        return true;
    }

    public IReadOnlyList<ProgressItemViewModel> BuildTranslatePlan() =>
        SelectedChapter is { } chapter
            ? [.. chapter.Translations.Select(translation => new ProgressItemViewModel($"Translate to {translation.LanguageCode.ToUpperInvariant()}"))]
            : [];

    public async Task TranslateChapterAsync(
        IProgress<ProgressTaskProgress> progress,
        CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            throw new InvalidOperationException("Another operation is in progress. Wait for it to finish.");
        }

        if (SelectedChapter is not { } chapter || chapter.Translations.Count == 0)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(chapter.ContentOriginal))
        {
            throw new InvalidOperationException("This chapter has no text yet. Generate or write it before translating.");
        }

        var translations = chapter.Translations.ToList();
        var completed = 0;
        var applied = 0;

        IsBusy = true;
        Status = "Translating…";

        try
        {
            for (var index = 0; index < translations.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var translation = translations[index];
                var stage = $"Translating {translation.LanguageCode.ToUpperInvariant()}…";
                Status = stage;
                progress.Report(new ProgressTaskProgress(completed, translations.Count, index, stage));

                var inner = new Progress<GenerationProgress>(report =>
                    progress.Report(new ProgressTaskProgress(completed, translations.Count, index, DescribeStage(report))));

                var text = await _translationService.TranslateAsync(chapter.ContentOriginal, translation.LanguageCode, inner, cancellationToken);
                ApplyTranslation(chapter, translation, text);
                applied++;
                IsDirty = true;
                completed++;
                progress.Report(new ProgressTaskProgress(completed, translations.Count, -1, string.Empty));
            }

            Status = "Translations updated.";
        }
        catch (OperationCanceledException)
        {
            Status = "Stopped.";
            throw;
        }
        catch (Exception exception)
        {
            Status = $"Failed: {exception.Message}";
            throw;
        }
        finally
        {
            IsBusy = false;

            if (applied > 0)
            {
                Mutated?.Invoke($"Translate chapter {chapter.Number}");
            }
        }
    }

    private async Task RegenerateSummaryAsync(ChapterViewModel chapter, IProgress<GenerationProgress>? progress, CancellationToken cancellationToken)
    {
        var project = ToProject();
        var target = project.Chapters.First(candidate => candidate.Number == chapter.Number);
        await _chapterRunner.RegenerateSummaryAsync(project, target, progress, cancellationToken);

        chapter.Logline = target.Logline;
        chapter.WorldState = target.WorldState;
        chapter.KnowledgeChanges = [.. target.KnowledgeChanges];
        MarkLaterStale(chapter.Number);
        IsDirty = true;
        RaiseMutation($"Regenerate summary of chapter {chapter.Number}");
    }

    private async Task CheckContinuityAsync(ChapterViewModel chapter, CancellationToken cancellationToken)
    {
        if (_continuityReviewer is null)
        {
            WarningRequested?.Invoke("Continuity check unavailable", "The continuity checker is not configured.");
            return;
        }

        var project = ToProject();
        var target = project.Chapters.First(candidate => candidate.Number == chapter.Number);
        var findings = await _continuityReviewer.ReviewAsync(project, target, cancellationToken);

        if (findings.Count == 0)
        {
            WarningRequested?.Invoke(
                $"Continuity check — chapter {chapter.Number}",
                "No contradictions found between this chapter's direction and the established facts.");
            return;
        }

        var message = string.Join("\n\n", findings.Select(finding =>
        {
            var reference = string.IsNullOrWhiteSpace(finding.Reference) ? string.Empty : $" ({finding.Reference})";
            return $"• [{finding.Severity}] {finding.Title}{reference}\n    {finding.Detail}";
        }));
        WarningRequested?.Invoke($"Continuity check — chapter {chapter.Number}", message);
    }

    private async Task<string> TranslateLanguageAsync(ChapterViewModel chapter, string languageCode, IProgress<GenerationProgress>? progress, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(chapter.ContentOriginal))
        {
            WarningRequested?.Invoke("Nothing to translate", "This chapter has no text yet. Generate or write it before translating.");
            return string.Empty;
        }

        var text = await _translationService.TranslateAsync(chapter.ContentOriginal, languageCode, progress, cancellationToken);
        var translation = chapter.Translations.FirstOrDefault(candidate => candidate.LanguageCode == languageCode);
        if (translation is not null)
        {
            ApplyTranslation(chapter, translation, text);
        }

        IsDirty = true;
        RaiseMutation($"Translate chapter {chapter.Number} ({languageCode.ToUpperInvariant()})");
        return text;
    }

    private void RaiseMutation(string name)
    {
        if (!_suppressMutation)
        {
            Mutated?.Invoke(name);
        }
    }

    private static void ApplyTranslation(ChapterViewModel chapter, TranslationViewModel translation, string text)
    {
        var tab = chapter.Tabs
            .FirstOrDefault(candidate => candidate.Header == translation.Header)?
            .Content as ChapterTextViewModel;

        if (tab is not null && !tab.IsBusy)
        {
            tab.Text = text;
            tab.IsStale = false;
        }
        else
        {
            translation.Text = text;
        }

        chapter.StaleTranslations.Remove(translation.LanguageCode);
    }

    private static void MarkTranslationsStale(ChapterViewModel chapter)
    {
        foreach (var translation in chapter.Translations)
        {
            if (!chapter.StaleTranslations.Contains(translation.LanguageCode))
            {
                chapter.StaleTranslations.Add(translation.LanguageCode);
            }
        }
    }

    public SetupViewModel CreateSetup(IReadOnlyList<LanguageData> catalog, ITextDiff textDiff, IGenerationAssistant assistant, IKnowledgeImporter importer, IProjectReviewAssistant review)
    {
        var setup = new SetupViewModel(textDiff, catalog, Languages, assistant, importer, review)
        {
            ProjectName = ProjectName,
            WorldTitle = _project.World.Title,
            WorldBody = _project.World.Body,
            Genre = _project.World.Genre,
            Tone = _project.World.Tone,
            Style = _project.World.Style,
            PointOfView = _project.World.PointOfView,
            Tense = _project.World.Tense,
            Rating = _project.World.Rating,
            InitialStateTimeAndPlace = _project.InitialWorldState.TimeAndPlace,
            InitialStateDescription = _project.InitialWorldState.Situation,
        };

        foreach (var entry in _project.Knowledge)
        {
            setup.Knowledge.Add(new KnowledgeEntryEditorViewModel(entry));
        }

        return setup;
    }

    public void ApplySetup(SetupViewModel setup)
    {
        ProjectName = setup.ProjectName;

        var before = SetupSignature(_project);
        var beforeName = _project.Name;
        var beforeBody = _project.World.Body;

        _project.Name = setup.ProjectName;
        _project.Settings.TargetLanguages = setup.SelectedLanguageCodes.ToList();
        _project.World.Title = setup.WorldTitle;
        _project.World.Body = setup.WorldBody;
        _project.World.Genre = setup.Genre;
        _project.World.Tone = setup.Tone;
        _project.World.Style = setup.Style;
        _project.World.PointOfView = setup.PointOfView;
        _project.World.Tense = setup.Tense;
        _project.World.Rating = setup.Rating;
        _project.Knowledge = [.. setup.Knowledge.Select(entry => entry.ToEntry())];
        _project.InitialWorldState.TimeAndPlace = setup.InitialStateTimeAndPlace;
        _project.InitialWorldState.Situation = setup.InitialStateDescription;

        UpdateLanguages(setup.SelectedLanguageCodes);

        var signatureChanged = !string.Equals(before, SetupSignature(_project), StringComparison.Ordinal);
        if (signatureChanged)
        {
            MarkAllStale();
        }

        if (!string.Equals(beforeName, _project.Name, StringComparison.Ordinal)
            || !string.Equals(beforeBody, _project.World.Body, StringComparison.Ordinal))
        {
            MarkMetadataStale();
        }

        IsDirty = true;
    }

    private void MarkAllStale()
    {
        foreach (var chapter in Chapters)
        {
            if (chapter.Status != ChapterStatus.Draft)
            {
                chapter.Status = ChapterStatus.Stale;
            }
        }
    }

    private void MarkMetadataStale()
    {
        foreach (var code in MetadataLanguages)
        {
            var alreadyMarked = _project.StaleMetadataTranslations
                .Any(stale => string.Equals(stale, code, StringComparison.OrdinalIgnoreCase));

            if (_project.MetadataTranslations.ContainsKey(code) && !alreadyMarked)
            {
                _project.StaleMetadataTranslations.Add(code);
            }
        }
    }

    private static string SetupSignature(Project project)
    {
        var builder = new StringBuilder();
        var world = project.World;
        builder.Append(world.Title).Append('|').Append(world.Body).Append('|').Append(world.Genre).Append('|')
            .Append(world.Tone).Append('|').Append(world.Style).Append('|').Append(world.PointOfView).Append('|')
            .Append(world.Tense).Append('|').Append(world.Rating).Append('|');
        builder.Append(project.InitialWorldState.TimeAndPlace).Append('|').Append(project.InitialWorldState.Situation).Append('|');
        foreach (var entry in project.Knowledge.OrderBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append(entry.Kind).Append(':').Append(entry.Title).Append(':').Append(entry.Content).Append(':')
                .Append(string.Join(',', entry.Tags)).Append('|');
        }

        return builder.ToString();
    }

    public Project ToProject()
    {
        _project.Name = ProjectName;
        _project.Settings.TargetLanguages = Languages.ToList();
        _project.Chapters = Chapters.Select(ToChapter).ToList();
        return _project;
    }

    public Task<IReadOnlyList<GenerationOption>> PlanChaptersAsync(int count, string brief, GenerationSession session, IProgress<GenerationProgress>? progress, CancellationToken cancellationToken)
    {
        var project = ToProject();
        var snapshot = WithKnowledge(project, KnowledgeComposer.Compose(project, 1), []);
        var request = new GenerationRequest
        {
            Target = GenerationTarget.ChapterPlan,
            Brief = brief,
            Variants = count,
            Bundle = true,
            Context = new GenerationContext { Fields = ProjectFields() },
            Snapshot = snapshot,
        };

        return _assistant.GenerateAsync(request, session, progress, cancellationToken);
    }

    public bool HasWrittenContent => Chapters.Any(chapter => !string.IsNullOrWhiteSpace(chapter.ContentOriginal));

    public void ApplyChapterPlan(IReadOnlyList<(string Title, string Direction)> plan)
    {
        Chapters.Clear();
        foreach (var (title, direction) in plan)
        {
            var chapter = new ChapterViewModel
            {
                Number = Chapters.Count + 1,
                Title = title,
                Direction = direction,
                Status = ChapterStatus.Draft,
                CreatedUtc = _clock.UtcNow,
            };

            foreach (var code in Languages)
            {
                chapter.Translations.Add(new TranslationViewModel(code, string.Empty));
            }

            BuildTabs(chapter);
            Chapters.Add(chapter);
        }

        if (Chapters.Count == 0)
        {
            AddNewChapter();
        }

        SelectedChapter = Chapters[0];
        SelectedTabIndex = 0;
        MarkMetadataStale();
        IsDirty = true;
        Mutated?.Invoke("Plan chapters");
    }

    private ChapterViewModel AddNewChapter()
    {
        var chapter = new ChapterViewModel
        {
            Number = Chapters.Count + 1,
            Title = $"Chapter {Chapters.Count + 1}",
            Status = ChapterStatus.Draft,
            CreatedUtc = _clock.UtcNow,
        };

        foreach (var code in Languages)
        {
            chapter.Translations.Add(new TranslationViewModel(code, string.Empty));
        }

        BuildTabs(chapter);
        Chapters.Add(chapter);
        return chapter;
    }

    private void UpdateLanguages(IReadOnlyList<string> codes)
    {
        if (Languages.SequenceEqual(codes))
        {
            return;
        }

        Languages.Clear();
        foreach (var code in codes)
        {
            Languages.Add(code);
        }

        foreach (var removed in _project.MetadataTranslations.Keys.Where(code => !Languages.Contains(code)).ToList())
        {
            _project.MetadataTranslations.Remove(removed);
        }

        _project.StaleMetadataTranslations.RemoveAll(code => !Languages.Contains(code));

        foreach (var chapter in Chapters)
        {
            foreach (var removed in chapter.TranslatedTitles.Keys.Where(code => !Languages.Contains(code)).ToList())
            {
                chapter.TranslatedTitles.Remove(removed);
            }

            EnsureTranslations(chapter);
            BuildTabs(chapter);
        }
    }

    private void EnsureTranslations(ChapterViewModel chapter)
    {
        for (var i = chapter.Translations.Count - 1; i >= 0; i--)
        {
            if (!Languages.Contains(chapter.Translations[i].LanguageCode))
            {
                chapter.Translations.RemoveAt(i);
            }
        }

        foreach (var code in Languages)
        {
            if (!chapter.Translations.Any(translation => translation.LanguageCode == code))
            {
                chapter.Translations.Add(new TranslationViewModel(code, string.Empty));
            }
        }
    }

    private void Renumber()
    {
        for (var i = 0; i < Chapters.Count; i++)
        {
            var chapter = Chapters[i];
            chapter.Number = i + 1;
            chapter.CanMoveUp = i > 0;
            chapter.CanMoveDown = i < Chapters.Count - 1;
            chapter.CanDelete = Chapters.Count > 1;
        }
    }

    private void BuildTabs(ChapterViewModel chapter)
    {
        chapter.Tabs.Clear();
        var text = new ChapterTextViewModel(
            chapter.ContentOriginal,
            isTranslation: false,
            value => chapter.ContentOriginal = value,
            () =>
            {
                MarkTranslationsStale(chapter);
                MarkLaterStale(chapter.Number);
                RaiseMutation($"Edit chapter {chapter.Number}");
            });
        chapter.PrimaryTextEditor = text;
        chapter.Tabs.Add(new ChapterTabViewModel("Chapter (EN)", text));

        foreach (var translation in chapter.Translations)
        {
            var captured = translation;
            var name = $"Edit chapter {chapter.Number} ({captured.LanguageCode.ToUpperInvariant()})";
            var translationTab = new ChapterTextViewModel(
                captured.Text,
                isTranslation: true,
                value => captured.Text = value,
                () => RaiseMutation(name))
            {
                IsStale = chapter.StaleTranslations.Contains(captured.LanguageCode),
                Translate = (progress, cancellationToken) => TranslateLanguageAsync(chapter, captured.LanguageCode, progress, cancellationToken),
            };
            chapter.Tabs.Add(new ChapterTabViewModel(captured.Header, translationTab));
        }

        var summary = new ChapterSummaryViewModel(chapter, () =>
        {
            MarkLaterStale(chapter.Number);
            RaiseMutation($"Edit summary of chapter {chapter.Number}");
        });
        summary.Regenerate = (progress, cancellationToken) => RegenerateSummaryAsync(chapter, progress, cancellationToken);
        summary.CheckContinuity = (_, cancellationToken) => CheckContinuityAsync(chapter, cancellationToken);
        chapter.Tabs.Add(new ChapterTabViewModel("Summary", summary));
        var committedTitle = chapter.Title;
        var settings = new ChapterSettingsViewModel(chapter, () =>
        {
            if (!string.Equals(committedTitle, chapter.Title, StringComparison.Ordinal))
            {
                committedTitle = chapter.Title;
                MarkMetadataStale();
            }

            RaiseMutation($"Edit settings of chapter {chapter.Number}");
        });
        settings.GenerateOptions = (brief, options, session, progress, cancellationToken) =>
            GenerateChapterAsync(chapter, brief, options, session, progress, cancellationToken);
        chapter.Tabs.Add(new ChapterTabViewModel("Settings", settings));
    }

    private Task<IReadOnlyList<GenerationOption>> GenerateChapterAsync(
        ChapterViewModel chapter,
        string brief,
        int options,
        GenerationSession session,
        IProgress<GenerationProgress>? progress,
        CancellationToken cancellationToken)
    {
        var project = ToProject();
        var priorChapters = project.Chapters.Where(candidate => candidate.Number < chapter.Number).ToList();
        var effective = WithKnowledge(project, KnowledgeComposer.Compose(project, chapter.Number), priorChapters);
        var request = new GenerationRequest
        {
            Target = GenerationTarget.ChapterSettings,
            Brief = brief,
            Variants = options,
            Context = new GenerationContext { Fields = ProjectFields() },
            Snapshot = effective,
            Avoid =
            [
                .. project.Chapters
                    .Where(candidate => candidate.Number != chapter.Number)
                    .Select(candidate => candidate.Title.Trim())
                    .Where(title => title.Length > 0),
            ],
        };

        return _assistant.GenerateAsync(request, session, progress, cancellationToken);
    }

    private IReadOnlyDictionary<string, string> ProjectFields() => new Dictionary<string, string>
    {
        ["ProjectName"] = _project.Name,
        ["WorldTitle"] = _project.World.Title,
        ["WorldBody"] = _project.World.Body,
        ["Genre"] = _project.World.Genre,
        ["Tone"] = _project.World.Tone,
        ["Style"] = _project.World.Style,
        ["PointOfView"] = _project.World.PointOfView,
        ["Tense"] = _project.World.Tense,
        ["Rating"] = _project.World.Rating,
    };

    private static Project WithKnowledge(Project project, IReadOnlyList<KnowledgeEntry> knowledge, IReadOnlyList<Chapter>? chapters = null) => new()
    {
        SchemaVersion = project.SchemaVersion,
        Id = project.Id,
        Name = project.Name,
        CreatedUtc = project.CreatedUtc,
        UpdatedUtc = project.UpdatedUtc,
        Settings = project.Settings,
        World = project.World,
        Knowledge = [.. knowledge],
        Chapters = [.. chapters ?? project.Chapters],
        InitialWorldState = project.InitialWorldState,
    };

    private static ChapterViewModel FromChapter(Chapter chapter)
    {
        var viewModel = new ChapterViewModel
        {
            Number = chapter.Number,
            Title = chapter.Title,
            Role = chapter.Role,
            Status = chapter.Status,
            ContentOriginal = chapter.ContentOriginal,
            Direction = chapter.Direction,
            Notes = chapter.Notes,
            Logline = chapter.Logline,
            CreatedUtc = chapter.CreatedUtc,
            WorldState = chapter.WorldState,
            KnowledgeChanges = [.. chapter.KnowledgeChanges],
            EditorNotes = [.. chapter.EditorNotes],
            StaleTranslations = [.. chapter.StaleTranslations],
        };

        foreach (var translation in chapter.Translations)
        {
            viewModel.Translations.Add(new TranslationViewModel(translation.Key, translation.Value));
        }

        viewModel.TranslatedTitles = new SortedDictionary<string, string>(chapter.TranslatedTitles);

        return viewModel;
    }

    private static Chapter ToChapter(ChapterViewModel viewModel) => new()
    {
        Number = viewModel.Number,
        Title = viewModel.Title,
        Role = viewModel.Role,
        Status = viewModel.Status,
        ContentOriginal = viewModel.ContentOriginal,
        Direction = viewModel.Direction,
        Notes = viewModel.Notes,
        Logline = viewModel.Logline,
        CreatedUtc = viewModel.CreatedUtc,
        WorldState = viewModel.WorldState,
        KnowledgeChanges = [.. viewModel.KnowledgeChanges],
        EditorNotes = [.. viewModel.EditorNotes],
        StaleTranslations = [.. viewModel.StaleTranslations],
        Translations = new SortedDictionary<string, string>(
            viewModel.Translations.ToDictionary(translation => translation.LanguageCode, translation => translation.Text)),
        TranslatedTitles = new SortedDictionary<string, string>(viewModel.TranslatedTitles),
    };
}
