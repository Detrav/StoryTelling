using System.Collections.ObjectModel;
using System.Linq;
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
using StoryTelling.Domain;

namespace StoryTelling.ViewModels;

public partial class WorkspaceViewModel : ViewModelBase
{
    private readonly Project _project;
    private readonly IClock _clock;
    private readonly IChapterAgent _chapterAgent;
    private CancellationTokenSource? _chapterCts;

    public WorkspaceViewModel(Project project, IClock clock, IChapterAgent chapterAgent)
    {
        _project = project;
        _clock = clock;
        _chapterAgent = chapterAgent;
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
    private void Stop() => _chapterCts?.Cancel();

    private async Task WriteChapterAsync()
    {
        if (IsBusy || SelectedChapter is not { } chapter)
        {
            return;
        }

        _chapterCts?.Dispose();
        _chapterCts = new CancellationTokenSource();
        var token = _chapterCts.Token;

        IsBusy = true;
        Status = "Writing…";

        var index = Chapters.IndexOf(chapter);
        var stateBefore = index > 0 ? Chapters[index - 1].WorldState ?? _project.WorldState : _project.WorldState;
        var context = new WriterContext(ToProject(), ToChapter(chapter), stateBefore, ChapterContextAssembler.DefaultTokenBudget);
        var editor = chapter.PrimaryTextEditor;
        editor?.BeginStream();

        var progress = new Progress<GenerationProgress>(report =>
            Status = report.ToolCalls > 0 ? $"{report.Stage}… ({report.ToolCalls} tool calls)" : $"{report.Stage}…");

        try
        {
            var draft = await _chapterAgent.WriteAsync(context, progress, delta =>
            {
                Dispatcher.UIThread.Post(() => editor?.AppendStreaming(delta));
                return Task.CompletedTask;
            }, token);

            editor?.EndStream(draft.Text);
            chapter.ContentOriginal = draft.Text;
            chapter.Status = ChapterStatus.Generated;
            Status = $"Chapter {chapter.Number} generated ({draft.ToolCalls} tool calls).";
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

    public SetupViewModel CreateSetup(IReadOnlyList<LanguageData> catalog, ITextDiff textDiff, IGenerationAssistant assistant, IKnowledgeImporter importer, IProjectReviewAssistant review)
    {
        var setup = new SetupViewModel(textDiff, catalog, Languages, assistant, importer, review)
        {
            ProjectName = ProjectName,
            WorldTitle = _project.Lore.Title,
            WorldBody = _project.Lore.Body,
            Genre = _project.Frame.Genre,
            Tone = _project.Frame.Tone,
            Style = _project.Frame.Style,
            PointOfView = _project.Frame.PointOfView,
            Tense = _project.Frame.Tense,
            Rating = _project.Frame.Rating,
            Premise = _project.Frame.Premise,
            Direction = _project.Frame.Direction,
            WorldStateTimeAndPlace = _project.WorldState.TimeAndPlace,
            WorldStateDescription = _project.WorldState.Description,
        };

        foreach (var character in _project.Characters)
        {
            setup.Characters.Add(new CharacterEditorViewModel(
                character.Id,
                character.Name,
                character.Role,
                character.Age,
                character.Description,
                character.Personality,
                character.Background,
                character.Goals,
                character.Traits));
        }

        foreach (var entry in _project.Knowledge)
        {
            setup.Knowledge.Add(new KnowledgeEntryEditorViewModel(entry));
        }

        return setup;
    }

    public void ApplySetup(SetupViewModel setup)
    {
        ProjectName = setup.ProjectName;

        _project.Name = setup.ProjectName;
        _project.Settings.TargetLanguages = setup.SelectedLanguageCodes.ToList();
        _project.Lore.Title = setup.WorldTitle;
        _project.Lore.Body = setup.WorldBody;
        _project.Characters = MergeCharacters(_project.Characters, setup.Characters);
        _project.Frame.Genre = setup.Genre;
        _project.Frame.Tone = setup.Tone;
        _project.Frame.Style = setup.Style;
        _project.Frame.PointOfView = setup.PointOfView;
        _project.Frame.Tense = setup.Tense;
        _project.Frame.Rating = setup.Rating;
        _project.Frame.Premise = setup.Premise;
        _project.Frame.Direction = setup.Direction;
        _project.Knowledge = [.. setup.Knowledge.Select(entry => entry.ToEntry())];
        _project.WorldState.TimeAndPlace = setup.WorldStateTimeAndPlace;
        _project.WorldState.Description = setup.WorldStateDescription;

        UpdateLanguages(setup.SelectedLanguageCodes);
        IsDirty = true;
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
            () => Mutated?.Invoke($"Edit chapter {chapter.Number}"));
        chapter.PrimaryTextEditor = text;
        chapter.Tabs.Add(new ChapterTabViewModel("Chapter (EN)", text));

        foreach (var translation in chapter.Translations)
        {
            var captured = translation;
            var name = $"Edit chapter {chapter.Number} ({captured.LanguageCode.ToUpperInvariant()})";
            chapter.Tabs.Add(new ChapterTabViewModel(
                captured.Header,
                new ChapterTextViewModel(
                    captured.Header,
                    captured.Text,
                    isTranslation: true,
                    value => captured.Text = value,
                    () => Mutated?.Invoke(name))));
        }

        chapter.Tabs.Add(new ChapterTabViewModel(
            "Summary",
            new ChapterSummaryViewModel(chapter, () => Mutated?.Invoke($"Edit summary of chapter {chapter.Number}"))));
        chapter.Tabs.Add(new ChapterTabViewModel(
            "Settings",
            new ChapterSettingsViewModel(chapter, () => Mutated?.Invoke($"Edit settings of chapter {chapter.Number}"))));
    }

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
            Summary = chapter.Summary,
            Logline = chapter.Logline,
            CreatedUtc = chapter.CreatedUtc,
            WorldState = chapter.WorldState,
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
        Summary = viewModel.Summary,
        Logline = viewModel.Logline,
        CreatedUtc = viewModel.CreatedUtc,
        WorldState = viewModel.WorldState,
        Translations = new SortedDictionary<string, string>(
            viewModel.Translations.ToDictionary(translation => translation.LanguageCode, translation => translation.Text)),
    };

    private static List<Character> MergeCharacters(IReadOnlyList<Character> existing, IEnumerable<CharacterEditorViewModel> editors)
    {
        var result = new List<Character>();
        foreach (var editor in editors)
        {
            var match = existing.FirstOrDefault(character => character.Id == editor.Id)
                ?? existing.FirstOrDefault(character => string.Equals(character.Name, editor.Name, StringComparison.Ordinal));

            var character = match ?? new Character { Id = editor.Id };
            character.Name = editor.Name;
            character.Role = editor.Role;
            character.Age = editor.Age;
            character.Description = editor.Description;
            character.Personality = editor.Personality;
            character.Background = editor.Background;
            character.Goals = editor.Goals;
            character.Traits = [.. editor.TraitList];
            result.Add(character);
        }

        return result;
    }
}
