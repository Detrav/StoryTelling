namespace StoryTelling.Domain;

public sealed class Character
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public List<string> Traits { get; set; } = [];

    public string Goals { get; set; } = string.Empty;
}
