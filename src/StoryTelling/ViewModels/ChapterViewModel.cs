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
    private string _summary = string.Empty;

    [ObservableProperty]
    private string _logline = string.Empty;

    [ObservableProperty]
    private DateTimeOffset _createdUtc;

    public WorldState? WorldState { get; set; }

    public ChapterTextViewModel? PrimaryTextEditor { get; set; }

    public ObservableCollection<TranslationViewModel> Translations { get; } = [];

    public ObservableCollection<ChapterTabViewModel> Tabs { get; } = [];

    public string StatusText => Status.ToString();

    partial void OnStatusChanged(ChapterStatus value) => OnPropertyChanged(nameof(StatusText));
}
