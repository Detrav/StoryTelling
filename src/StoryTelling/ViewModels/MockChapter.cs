using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace StoryTelling.ViewModels;

public partial class MockChapter : ObservableObject
{
    public MockChapter(int number, string title, string status, string content)
    {
        _number = number;
        _title = title;
        _status = status;
        _content = content;
    }

    [ObservableProperty]
    private int _number;

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private string _status;

    [ObservableProperty]
    private string _content;

    [ObservableProperty]
    private string _direction = string.Empty;

    [ObservableProperty]
    private string _notes = string.Empty;

    [ObservableProperty]
    private string _summaryRecap = string.Empty;

    [ObservableProperty]
    private string _summaryLogline = string.Empty;

    public ObservableCollection<ChapterTranslation> Translations { get; } = [];

    public ObservableCollection<ChapterTabViewModel> Tabs { get; } = [];
}
