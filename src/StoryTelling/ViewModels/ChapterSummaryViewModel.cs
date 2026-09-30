using System.ComponentModel;

namespace StoryTelling.ViewModels;

public partial class ChapterSummaryViewModel : ViewModelBase
{
    private readonly MockChapter _chapter;

    public ChapterSummaryViewModel(MockChapter chapter)
    {
        _chapter = chapter;
        chapter.PropertyChanged += OnChapterChanged;
    }

    public string Recap
    {
        get => _chapter.SummaryRecap;
        set => _chapter.SummaryRecap = value;
    }

    public string Logline
    {
        get => _chapter.SummaryLogline;
        set => _chapter.SummaryLogline = value;
    }

    private void OnChapterChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MockChapter.SummaryRecap))
        {
            OnPropertyChanged(nameof(Recap));
        }
        else if (e.PropertyName == nameof(MockChapter.SummaryLogline))
        {
            OnPropertyChanged(nameof(Logline));
        }
    }
}
