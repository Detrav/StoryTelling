using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Avalonia.Threading;
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
    private CancellationTokenSource? _chapterCts;

    public WorkspaceViewModel(Project project, IClock clock, IChapterRunner chapterRunner, IGenerationAssistant assistant, ITranslationService translationService)
    {
        _project = project;
        _clock = clock;
        _chapterRunner = chapterRunner;
        _assistant = assistant;
        _translationService = translationService;
        _projectName = project.Name;

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

    [RelayCommand]
    private Task Generate() => WriteChapterAsync();

    [RelayCommand]
    private Task Regenerate() => WriteChapterAsync();

    [RelayCommand]
    private Task WriteNextChapter()
    {
        AddChapter();
        return WriteChapterAsync();
    }

    [RelayCommand]
    private void Stop() => _chapterCts?.Cancel();

    private async Task WriteChapterAsync()
    {
        if (IsBusy || SelectedChapter is not { } chapter)
        {
            return;
        }

        var missing = MissingForGeneration(chapter);
        if (missing.Count > 0)
        {
            WarningRequested?.Invoke(
                $"Cannot generate chapter {chapter.Number}",
                "Fill in the following first:\n• " + string.Join("\n• ", missing));
            return;
        }

        _chapterCts?.Dispose();
        _chapterCts = new CancellationTokenSource();
        var token = _chapterCts.Token;

        IsBusy = true;
        Status = "Writing…";

        var editor = chapter.PrimaryTextEditor;
        editor?.BeginStream();

        var progress = new Progress<GenerationProgress>(report =>
            Status = report.ToolCalls > 0 ? $"{report.Stage}… ({report.ToolCalls} tool calls)" : $"{report.Stage}…");

        try
        {
            var project = ToProject();
            var target = project.Chapters.First(candidate => candidate.Number == chapter.Number);
            var result = await _chapterRunner.GenerateAsync(project, target, progress, delta =>
            {
                Dispatcher.UIThread.Post(() => editor?.AppendStreaming(delta));
                return Task.CompletedTask;
            }, token);

            editor?.EndStream(result.Text);
            chapter.ContentOriginal = result.Text;
            chapter.Logline = result.Logline;
            chapter.WorldState = result.WorldState;
            chapter.KnowledgeChanges = [.. result.KnowledgeChanges];
            chapter.EditorNotes = [.. result.EditorNotes];
            MarkTranslationsStale(chapter);
            chapter.Status = ChapterStatus.Generated;
            MarkLaterStale(chapter.Number);
            Status = $"Chapter {chapter.Number} generated ({result.ToolCalls} tool calls).";
            Mutated?.Invoke($"Generate chapter {chapter.Number}");
        }
        catch (OperationCanceledException)
        {
            Status = "Stopped.";
        }
        catch (LlmException exception)
        {
            Status = $"Failed ({exception.Kind}): {exception.Message}";
        }
        catch (Exception exception)
        {
            Status = $"Failed: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

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
        return string.IsNullOrWhiteSpace(state.TimeAndPlace) && string.IsNullOrWhiteSpace(state.Description);
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

    [RelayCommand]
    private async Task TranslateChapter()
    {
        if (IsBusy || SelectedChapter is not { } chapter || chapter.Translations.Count == 0)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(chapter.ContentOriginal))
        {
            WarningRequested?.Invoke(
                "Nothing to translate",
                "This chapter has no text yet. Generate or write it before translating.");
            return;
        }

        _chapterCts?.Dispose();
        _chapterCts = new CancellationTokenSource();
        var token = _chapterCts.Token;

        IsBusy = true;
        Status = "Translating…";

        try
        {
            foreach (var translation in chapter.Translations)
            {
                token.ThrowIfCancellationRequested();
                Status = $"Translating {translation.LanguageCode.ToUpperInvariant()}…";
                var text = await _translationService.TranslateAsync(chapter.ContentOriginal, translation.LanguageCode, null, token);
                ApplyTranslation(chapter, translation, text);
            }

            IsDirty = true;
            Status = "Translations updated.";
            Mutated?.Invoke($"Translate chapter {chapter.Number}");
        }
        catch (OperationCanceledException)
        {
            Status = "Stopped.";
        }
        catch (Exception exception)
        {
            Status = $"Failed: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
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
        Mutated?.Invoke($"Regenerate summary of chapter {chapter.Number}");
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
        Mutated?.Invoke($"Translate chapter {chapter.Number} ({languageCode.ToUpperInvariant()})");
        return text;
    }

    private static void ApplyTranslation(ChapterViewModel chapter, TranslationViewModel translation, string text)
    {
        var tab = chapter.Tabs
            .Select(candidate => candidate.Content)
            .OfType<ChapterTextViewModel>()
            .FirstOrDefault(candidate => candidate.Header == translation.Header);

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
            InitialStateDescription = _project.InitialWorldState.Description,
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
        _project.InitialWorldState.Description = setup.InitialStateDescription;

        UpdateLanguages(setup.SelectedLanguageCodes);

        if (!string.Equals(before, SetupSignature(_project), StringComparison.Ordinal))
        {
            MarkAllStale();
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

    private static string SetupSignature(Project project)
    {
        var builder = new StringBuilder();
        var world = project.World;
        builder.Append(world.Title).Append('|').Append(world.Body).Append('|').Append(world.Genre).Append('|')
            .Append(world.Tone).Append('|').Append(world.Style).Append('|').Append(world.PointOfView).Append('|')
            .Append(world.Tense).Append('|').Append(world.Rating).Append('|');
        builder.Append(project.InitialWorldState.TimeAndPlace).Append('|').Append(project.InitialWorldState.Description).Append('|');
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

        foreach (var chapter in Chapters)
        {
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
            Chapters[i].Number = i + 1;
        }
    }

    private void BuildTabs(ChapterViewModel chapter)
    {
        chapter.Tabs.Clear();
        var text = new ChapterTextViewModel(
            "Chapter (EN)",
            chapter.ContentOriginal,
            isTranslation: false,
            value => chapter.ContentOriginal = value,
            () =>
            {
                MarkTranslationsStale(chapter);
                MarkLaterStale(chapter.Number);
                Mutated?.Invoke($"Edit chapter {chapter.Number}");
            });
        chapter.PrimaryTextEditor = text;
        chapter.Tabs.Add(new ChapterTabViewModel("Chapter (EN)", text));

        foreach (var translation in chapter.Translations)
        {
            var captured = translation;
            var name = $"Edit chapter {chapter.Number} ({captured.LanguageCode.ToUpperInvariant()})";
            var translationTab = new ChapterTextViewModel(
                captured.Header,
                captured.Text,
                isTranslation: true,
                value => captured.Text = value,
                () => Mutated?.Invoke(name))
            {
                IsStale = chapter.StaleTranslations.Contains(captured.LanguageCode),
                Translate = (progress, cancellationToken) => TranslateLanguageAsync(chapter, captured.LanguageCode, progress, cancellationToken),
            };
            chapter.Tabs.Add(new ChapterTabViewModel(captured.Header, translationTab));
        }

        var summary = new ChapterSummaryViewModel(chapter, () =>
        {
            MarkLaterStale(chapter.Number);
            Mutated?.Invoke($"Edit summary of chapter {chapter.Number}");
        });
        summary.Regenerate = (progress, cancellationToken) => RegenerateSummaryAsync(chapter, progress, cancellationToken);
        chapter.Tabs.Add(new ChapterTabViewModel("Summary", summary));
        var settings = new ChapterSettingsViewModel(chapter, () => Mutated?.Invoke($"Edit settings of chapter {chapter.Number}"));
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

        return viewModel;
    }

    private static Chapter ToChapter(ChapterViewModel viewModel) => new()
    {
        Number = viewModel.Number,
        Title = viewModel.Title,
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
    };
}
