using System.ComponentModel;
using StoryTelling.Application.Generation;
using StoryTelling.Domain;

namespace StoryTelling.ViewModels;

public partial class ChapterSettingsViewModel : ViewModelBase
{
    private readonly ChapterViewModel _chapter;
    private readonly CommitDebouncer _debouncer;

    public ChapterSettingsViewModel(ChapterViewModel chapter, Action commit)
    {
        _chapter = chapter;
        _debouncer = new CommitDebouncer(commit, TimeSpan.FromMilliseconds(700));
        chapter.PropertyChanged += OnChapterChanged;
    }

    public delegate Task<IReadOnlyList<GenerationOption>> SuggestOptions(
        ChapterRole role,
        string notes,
        int variants,
        GenerationSession session,
        IProgress<GenerationProgress>? progress,
        CancellationToken cancellationToken);

    public SuggestOptions? Suggest { get; set; }

    public IReadOnlyList<ChapterRole> Roles { get; } = Enum.GetValues<ChapterRole>();

    public ChapterRole Role
    {
        get => _chapter.Role;
        set
        {
            if (_chapter.Role == value)
            {
                return;
            }

            _chapter.Role = value;
            _debouncer.Trigger();
        }
    }

    public string Title
    {
        get => _chapter.Title;
        set
        {
            if (_chapter.Title == value)
            {
                return;
            }

            _chapter.Title = value;
            _debouncer.Trigger();
        }
    }

    public string Direction
    {
        get => _chapter.Direction;
        set
        {
            if (_chapter.Direction == value)
            {
                return;
            }

            _chapter.Direction = value;
            _debouncer.Trigger();
        }
    }

    public string Notes
    {
        get => _chapter.Notes;
        set
        {
            if (_chapter.Notes == value)
            {
                return;
            }

            _chapter.Notes = value;
            _debouncer.Trigger();
        }
    }

    public string Status => _chapter.StatusText;

    public bool HasDirectionSuggestion => _chapter.HasDirectionSuggestions;

    public string DirectionSuggestion => _chapter.DirectionSuggestions.Count > 0 ? _chapter.DirectionSuggestions[0] : string.Empty;

    public void ApplyDirectionSuggestion()
    {
        if (_chapter.DirectionSuggestions.Count == 0)
        {
            return;
        }

        Direction = _chapter.DirectionSuggestions[0];
    }

    public void DismissDirectionSuggestion()
    {
        _chapter.DirectionSuggestions = [];
        _debouncer.Trigger();
    }

    public IReadOnlyList<TranslatedTitleViewModel> TranslatedTitles =>
        _chapter.TranslatedTitles
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .Select(pair => new TranslatedTitleViewModel(pair.Key.ToUpperInvariant(), pair.Value))
            .ToList();

    public bool HasTranslatedTitles => TranslatedTitles.Count > 0;

    public string Created => _chapter.CreatedUtc == default
        ? "—"
        : _chapter.CreatedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    public void Commit() => _debouncer.CommitNow();

    public void ApplyChapterSetup(ChapterRole role, string notes, string title, string direction)
    {
        Role = role;
        Notes = notes;
        Title = title;
        Direction = direction;
        _debouncer.Trigger();
    }

    private void OnChapterChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ChapterViewModel.Title):
                OnPropertyChanged(nameof(Title));
                break;
            case nameof(ChapterViewModel.Role):
                OnPropertyChanged(nameof(Role));
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
            case nameof(ChapterViewModel.DirectionSuggestions):
                OnPropertyChanged(nameof(HasDirectionSuggestion));
                OnPropertyChanged(nameof(DirectionSuggestion));
                break;
        }
    }
}
