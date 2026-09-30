using System.Collections.ObjectModel;
using System.ComponentModel;
using StoryTelling.Domain;

namespace StoryTelling.ViewModels;

public partial class ChapterSettingsViewModel : ViewModelBase
{
    private readonly ChapterViewModel _chapter;
    private readonly CommitDebouncer _debouncer;
    private readonly List<CharacterParticipationViewModel> _participants;

    public ChapterSettingsViewModel(ChapterViewModel chapter, IReadOnlyList<Character> characters, Action commit)
    {
        _chapter = chapter;
        _debouncer = new CommitDebouncer(commit, TimeSpan.FromMilliseconds(700));
        chapter.PropertyChanged += OnChapterChanged;

        _participants = BuildParticipants(chapter, characters);
        Participants = new ObservableCollection<CharacterParticipationViewModel>(_participants);
        foreach (var participant in _participants)
        {
            participant.PropertyChanged += OnParticipationChanged;
        }
    }

    public ObservableCollection<CharacterParticipationViewModel> Participants { get; }

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

    public string LabelFor(string field) => field switch
    {
        "Title" => "Chapter title",
        "Direction" => "Chapter direction",
        _ => field,
    };

    public void Commit() => _debouncer.CommitNow();

    private static List<CharacterParticipationViewModel> BuildParticipants(ChapterViewModel chapter, IReadOnlyList<Character> characters)
    {
        var participants = new List<CharacterParticipationViewModel>();
        foreach (var character in characters)
        {
            var link = chapter.Characters.FirstOrDefault(existing => existing.CharacterId == character.Id);
            participants.Add(new CharacterParticipationViewModel(
                character.Id,
                string.IsNullOrWhiteSpace(character.Name) ? "(unnamed)" : character.Name,
                link?.Presence == CharacterPresence.Full,
                link?.Presence == CharacterPresence.NameOnly));
        }

        return participants;
    }

    private void OnParticipationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(CharacterParticipationViewModel.IsFull) or nameof(CharacterParticipationViewModel.IsNameOnly)))
        {
            return;
        }

        _chapter.Characters = [.. _participants.Select(participant => participant.ToChapterCharacter()).OfType<ChapterCharacter>()];
        _debouncer.Trigger();
    }

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
