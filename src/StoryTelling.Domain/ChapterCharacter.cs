namespace StoryTelling.Domain;

public sealed class ChapterCharacter
{
    public Guid CharacterId { get; set; }

    public CharacterPresence Presence { get; set; } = CharacterPresence.Full;
}
