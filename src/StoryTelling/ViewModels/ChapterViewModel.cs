using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using StoryTelling.Domain;

namespace StoryTelling.ViewModels;

public partial class ChapterViewModel : ObservableObject
{
    [ObservableProperty]
    private int _number;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private ChapterStatus _status;

    [ObservableProperty]
    private string _contentOriginal = string.Empty;

    [ObservableProperty]
    private string _direction = string.Empty;

    [ObservableProperty]
    private string _notes = string.Empty;

    [ObservableProperty]
    private string _logline = string.Empty;

    [ObservableProperty]
    private DateTimeOffset _createdUtc;

    [ObservableProperty]
    private WorldState? _worldState;

    [ObservableProperty]
    private List<KnowledgeChange> _knowledgeChanges = [];

    [ObservableProperty]
    private List<EditorNote> _editorNotes = [];

    [ObservableProperty]
    private List<string> _staleTranslations = [];

    [ObservableProperty]
    private bool _canMoveUp;

    [ObservableProperty]
    private bool _canMoveDown;

    [ObservableProperty]
    private bool _canDelete;

    public SortedDictionary<string, string> TranslatedTitles { get; set; } = [];

    public ChapterTextViewModel? PrimaryTextEditor { get; set; }

    public ObservableCollection<TranslationViewModel> Translations { get; } = [];

    public ObservableCollection<ChapterTabViewModel> Tabs { get; } = [];

    public string StatusText => Status.ToString();

    public bool IsDraft => Status == ChapterStatus.Draft;

    public bool IsGenerated => Status == ChapterStatus.Generated;

    public bool IsStale => Status == ChapterStatus.Stale;

    public string StatusHint => Status switch
    {
        ChapterStatus.Draft => "Planned but not written yet — the AI will write it.",
        ChapterStatus.Edited => "Hand-edited after generation.",
        ChapterStatus.Stale => "Out of date — an earlier chapter or the setup changed. Regenerate it, or set it to Generated if the text is still fine.",
        _ => "The text is up to date.",
    };

    partial void OnStatusChanged(ChapterStatus value)
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(IsDraft));
        OnPropertyChanged(nameof(IsGenerated));
        OnPropertyChanged(nameof(IsStale));
        OnPropertyChanged(nameof(StatusHint));
    }
}
