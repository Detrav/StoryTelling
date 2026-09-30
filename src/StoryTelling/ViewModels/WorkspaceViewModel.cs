using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace StoryTelling.ViewModels;

public partial class WorkspaceViewModel : ViewModelBase
{
    private static readonly string[] _seedTitles =
    [
        "Embers",
        "Ashes",
        "The Long Night",
        "Broken Oaths",
        "The Ember Crown",
    ];

    private static readonly string[] _seedDirections =
    [
        "Introduce Aria and the burning keep.",
        "She flees into the frontier; first bargain with Bran.",
        "The relic is found — and is not what it seems.",
        "The rebellion fractures; a betrayal.",
        "Final confrontation and the cost of victory.",
    ];

    private int _chapterCount = 5;

    public event Action? CloseRequested;

    public WorkspaceViewModel(LanguageCatalog catalog)
    {
        Languages.Add(catalog.DefaultCode);
        SeedChapters();
        _selectedChapter = Chapters[0];
    }

    [ObservableProperty]
    private string _projectName = "The Ember Crown";

    [ObservableProperty]
    private bool _isDirty = true;

    [ObservableProperty]
    private bool _isSidebarVisible = true;

    [ObservableProperty]
    private MockChapter _selectedChapter;

    public ObservableCollection<string> Languages { get; } = [];

    public ObservableCollection<MockChapter> Chapters { get; } = [];

    [RelayCommand]
    private void AddChapter()
    {
        var chapter = new MockChapter(Chapters.Count + 1, $"Chapter {Chapters.Count + 1}", "Draft", string.Empty);
        AddTranslations(chapter);
        BuildTabs(chapter);
        Chapters.Add(chapter);
        SelectedChapter = chapter;
        _chapterCount = Chapters.Count;
        IsDirty = true;
    }

    [RelayCommand]
    private void DeleteChapter(MockChapter? chapter)
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
        _chapterCount = Chapters.Count;
        IsDirty = true;
    }

    [RelayCommand]
    private void MoveChapterUp(MockChapter? chapter)
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
    }

    [RelayCommand]
    private void MoveChapterDown(MockChapter? chapter)
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

    public SetupViewModel CreateSetup(LanguageCatalog catalog)
    {
        var setup = new SetupViewModel(catalog)
        {
            ProjectName = ProjectName,
            ChapterCount = Chapters.Count,
        };

        foreach (var selection in setup.LanguageSelections)
        {
            selection.IsSelected = Languages.Contains(selection.Option.Code);
        }

        return setup;
    }

    public void ApplySetup(SetupViewModel setup)
    {
        ProjectName = setup.ProjectName;

        var codes = setup.SelectedLanguageCodes;
        var languagesChanged = !codes.SequenceEqual(Languages);
        Languages.Clear();
        foreach (var code in codes)
        {
            Languages.Add(code);
        }

        if (setup.ChapterCount != Chapters.Count || languagesChanged)
        {
            _chapterCount = setup.ChapterCount;
            SeedChapters();
            SelectedChapter = Chapters[0];
        }

        IsDirty = true;
    }

    private void SeedChapters()
    {
        Chapters.Clear();
        for (var i = 0; i < _chapterCount; i++)
        {
            var title = i < _seedTitles.Length ? _seedTitles[i] : $"Chapter {i + 1}";
            var direction = i < _seedDirections.Length ? _seedDirections[i] : string.Empty;
            var generated = i < 2;
            var chapter = new MockChapter(
                i + 1,
                title,
                generated ? "Generated" : "Draft",
                generated ? "The smoke rose over the keep long before the bells." : string.Empty)
            {
                Direction = direction,
                SummaryRecap = generated ? "Aria escapes the burning keep; the relic is real." : string.Empty,
                SummaryLogline = generated ? "A scout flees a burning keep." : string.Empty,
            };

            AddTranslations(chapter);
            BuildTabs(chapter);
            Chapters.Add(chapter);
        }
    }

    private void AddTranslations(MockChapter chapter)
    {
        foreach (var code in Languages)
        {
            var text = string.IsNullOrEmpty(chapter.Content) ? string.Empty : $"({code}) translation of chapter {chapter.Number}.";
            chapter.Translations.Add(new ChapterTranslation(code, text));
        }
    }

    private static void BuildTabs(MockChapter chapter)
    {
        chapter.Tabs.Clear();
        chapter.Tabs.Add(new ChapterTabViewModel(
            "Chapter (EN)",
            new ChapterTextViewModel("Chapter (EN)", chapter.Content, isTranslation: false, value => chapter.Content = value)));

        foreach (var translation in chapter.Translations)
        {
            var captured = translation;
            chapter.Tabs.Add(new ChapterTabViewModel(
                captured.Header,
                new ChapterTextViewModel(captured.Header, captured.Text, isTranslation: true, value => captured.Text = value)));
        }

        chapter.Tabs.Add(new ChapterTabViewModel("Summary", new ChapterSummaryViewModel(chapter)));
        chapter.Tabs.Add(new ChapterTabViewModel("Settings", new ChapterSettingsViewModel(chapter)));
    }

    private void Renumber()
    {
        for (var i = 0; i < Chapters.Count; i++)
        {
            Chapters[i].Number = i + 1;
        }
    }
}
