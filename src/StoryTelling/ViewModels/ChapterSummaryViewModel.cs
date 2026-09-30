using System.ComponentModel;

namespace StoryTelling.ViewModels;

public partial class ChapterSummaryViewModel : ViewModelBase
{
    private readonly ChapterViewModel _chapter;

    public ChapterSummaryViewModel(ChapterViewModel chapter)
    {
        _chapter = chapter;
        chapter.PropertyChanged += OnChapterChanged;
    }

    public string Recap
    {
        get => _chapter.Summary;
        set => _chapter.Summary = value;
    }

    public string Logline
    {
        get => _chapter.Logline;
        set => _chapter.Logline = value;
    }

    private void OnChapterChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChapterViewModel.Summary))
        {
            OnPropertyChanged(nameof(Recap));
        }
        else if (e.PropertyName == nameof(ChapterViewModel.Logline))
        {
            OnPropertyChanged(nameof(Logline));
        }
    }
}
