using System.ComponentModel;

namespace StoryTelling.ViewModels;

public partial class ChapterSettingsViewModel : ViewModelBase
{
    private readonly ChapterViewModel _chapter;

    public ChapterSettingsViewModel(ChapterViewModel chapter)
    {
        _chapter = chapter;
        chapter.PropertyChanged += OnChapterChanged;
    }

    public string Title
    {
        get => _chapter.Title;
        set => _chapter.Title = value;
    }

    public string Direction
    {
        get => _chapter.Direction;
        set => _chapter.Direction = value;
    }

    public string Notes
    {
        get => _chapter.Notes;
        set => _chapter.Notes = value;
    }

    public string Status => _chapter.StatusText;

    public string LabelFor(string field) => field switch
    {
        "Title" => "Chapter title",
        "Direction" => "Chapter direction",
        _ => field,
    };

    public void ApplyGenerated(string field, string text)
    {
        switch (field)
        {
            case "Title":
                Title = text.Split('\n')[0].Trim();
                break;
            case "Direction":
                Direction = text;
                break;
        }
    }

    private void OnChapterChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ChapterViewModel.Title):
                OnPropertyChanged(nameof(Title));
                break;
            case nameof(ChapterViewModel.Direction):
                OnPropertyChanged(nameof(Direction));
                break;
            case nameof(ChapterViewModel.Notes):
                OnPropertyChanged(nameof(Notes));
                break;
            case nameof(ChapterViewModel.Status):
                OnPropertyChanged(nameof(Status));
                break;
        }
    }
}
