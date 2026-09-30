using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StoryTelling.Application.Abstractions;
using StoryTelling.Application.Settings;
using StoryTelling.Domain;

namespace StoryTelling.ViewModels;

public partial class WorkspaceViewModel : ViewModelBase
{
    private readonly Project _project;
    private readonly IClock _clock;

    public WorkspaceViewModel(Project project, IClock clock)
    {
        _project = project;
        _clock = clock;
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
            var count = Math.Max(1, project.Plot.ChapterCount);
            for (var i = 0; i < count; i++)
            {
                AddNewChapter();
            }
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
    private void Generate()
    {
    }

    [RelayCommand]
    private void Regenerate()
    {
    }

    [RelayCommand]
    private void Stop()
    {
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke();

    public SetupViewModel CreateSetup(IReadOnlyList<LanguageData> catalog, ITextDiff textDiff)
    {
        var setup = new SetupViewModel(textDiff, catalog, Languages)
        {
            ProjectName = ProjectName,
            WorldTitle = _project.Lore.Title,
            WorldBody = _project.Lore.Body,
            Genre = _project.Plot.Genre,
            Tone = _project.Plot.Tone,
            Premise = _project.Plot.Premise,
            Direction = _project.Plot.Direction,
            WorldStateTimeAndPlace = _project.WorldState.TimeAndPlace,
        };

        foreach (var character in _project.Characters)
        {
            setup.Characters.Add(FormatCharacter(character));
        }

        foreach (var file in _project.ExtraFiles)
        {
            setup.ExtraFiles.Add(file.Name);
        }

        foreach (var state in _project.WorldState.Characters)
        {
            setup.WorldStateCharacters.Add(state.Name);
        }

        foreach (var thread in _project.WorldState.ActiveThreads)
        {
            setup.WorldStateThreads.Add(thread);
        }

        foreach (var item in _project.WorldState.Items)
        {
            setup.WorldStateItems.Add(item);
        }

        foreach (var question in _project.WorldState.OpenQuestions)
        {
            setup.WorldStateOpenQuestions.Add(question);
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
        _project.Plot.Genre = setup.Genre;
        _project.Plot.Tone = setup.Tone;
        _project.Plot.Premise = setup.Premise;
        _project.Plot.Direction = setup.Direction;
        _project.ExtraFiles = MergeExtraFiles(_project.ExtraFiles, setup.ExtraFiles);
        _project.WorldState.TimeAndPlace = setup.WorldStateTimeAndPlace;
        _project.WorldState.Characters = MergeCharacterStates(_project.WorldState.Characters, setup.WorldStateCharacters);
        _project.WorldState.ActiveThreads = setup.WorldStateThreads.ToList();
        _project.WorldState.Items = setup.WorldStateItems.ToList();
        _project.WorldState.OpenQuestions = setup.WorldStateOpenQuestions.ToList();

        UpdateLanguages(setup.SelectedLanguageCodes);
        _project.Plot.ChapterCount = Chapters.Count;
        IsDirty = true;
    }

    public Project ToProject()
    {
        _project.Name = ProjectName;
        _project.Settings.TargetLanguages = Languages.ToList();
        _project.Plot.ChapterCount = Chapters.Count;
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
        chapter.Tabs.Add(new ChapterTabViewModel(
            "Chapter (EN)",
            new ChapterTextViewModel(
                "Chapter (EN)",
                chapter.ContentOriginal,
                isTranslation: false,
                value => chapter.ContentOriginal = value,
                () => Mutated?.Invoke($"Edit chapter {chapter.Number}"))));

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
        Translations = new SortedDictionary<string, string>(
            viewModel.Translations.ToDictionary(translation => translation.LanguageCode, translation => translation.Text)),
    };

    private static string FormatCharacter(Character character) =>
        string.IsNullOrWhiteSpace(character.Description) ? character.Name : $"{character.Name} — {character.Description}";

    private static List<Character> MergeCharacters(IReadOnlyList<Character> existing, IEnumerable<string> lines)
    {
        var result = new List<Character>();
        foreach (var line in lines)
        {
            var parsed = ParseCharacter(line);
            var match = existing.FirstOrDefault(character => string.Equals(character.Name, parsed.Name, StringComparison.Ordinal));
            if (match is not null)
            {
                match.Name = parsed.Name;
                match.Description = parsed.Description;
                result.Add(match);
            }
            else
            {
                result.Add(parsed);
            }
        }

        return result;
    }

    private static List<ExtraFile> MergeExtraFiles(IReadOnlyList<ExtraFile> existing, IEnumerable<string> names)
    {
        var result = new List<ExtraFile>();
        foreach (var name in names)
        {
            var match = existing.FirstOrDefault(file => string.Equals(file.Name, name, StringComparison.Ordinal));
            result.Add(match ?? new ExtraFile { Name = name });
        }

        return result;
    }

    private static List<CharacterState> MergeCharacterStates(IReadOnlyList<CharacterState> existing, IEnumerable<string> names)
    {
        var result = new List<CharacterState>();
        foreach (var name in names)
        {
            var match = existing.FirstOrDefault(state => string.Equals(state.Name, name, StringComparison.Ordinal));
            result.Add(match ?? new CharacterState { Name = name });
        }

        return result;
    }

    private static Character ParseCharacter(string text)
    {
        var separator = text.IndexOf(" — ", StringComparison.Ordinal);
        if (separator < 0)
        {
            return new Character { Name = text.Trim() };
        }

        return new Character
        {
            Name = text[..separator].Trim(),
            Description = text[(separator + 3)..].Trim(),
        };
    }
}
