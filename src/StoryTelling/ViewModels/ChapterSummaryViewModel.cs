using System.ComponentModel;

namespace StoryTelling.ViewModels;

public partial class ChapterSummaryViewModel : ViewModelBase
{
    private readonly ChapterViewModel _chapter;
    private readonly CommitDebouncer _debouncer;

    public ChapterSummaryViewModel(ChapterViewModel chapter, Action commit)
    {
        _chapter = chapter;
        _debouncer = new CommitDebouncer(commit, TimeSpan.FromMilliseconds(700));
        chapter.PropertyChanged += OnChapterChanged;
    }

    public string Recap
    {
        get => _chapter.Summary;
        set
        {
            if (_chapter.Summary == value)
            {
                return;
            }

            _chapter.Summary = value;
            _debouncer.Trigger();
        }
    }

    public string Logline
    {
        get => _chapter.Logline;
        set
        {
            if (_chapter.Logline == value)
            {
                return;
            }

            _chapter.Logline = value;
            _debouncer.Trigger();
        }
    }

    public void Commit() => _debouncer.CommitNow();

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
