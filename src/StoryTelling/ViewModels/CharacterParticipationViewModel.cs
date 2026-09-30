using CommunityToolkit.Mvvm.ComponentModel;
using StoryTelling.Domain;

namespace StoryTelling.ViewModels;

public partial class CharacterParticipationViewModel : ObservableObject
{
    public CharacterParticipationViewModel(Guid characterId, string name, bool isFull, bool isNameOnly)
    {
        CharacterId = characterId;
        Name = name;
        _isFull = isFull;
        _isNameOnly = isNameOnly;
    }

    public Guid CharacterId { get; }

    public string Name { get; }

    [ObservableProperty]
    private bool _isFull;

    [ObservableProperty]
    private bool _isNameOnly;

    public ChapterCharacter? ToChapterCharacter() =>
        IsFull ? new ChapterCharacter { CharacterId = CharacterId, Presence = CharacterPresence.Full }
        : IsNameOnly ? new ChapterCharacter { CharacterId = CharacterId, Presence = CharacterPresence.NameOnly }
        : null;

    partial void OnIsFullChanged(bool value)
    {
        if (value)
        {
            IsNameOnly = false;
        }
    }

    partial void OnIsNameOnlyChanged(bool value)
    {
        if (value)
        {
            IsFull = false;
        }
    }
}
