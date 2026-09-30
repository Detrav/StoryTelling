namespace StoryTelling.Domain;

public sealed class Character
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string Age { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Personality { get; set; } = string.Empty;

    public string Background { get; set; } = string.Empty;

    public string Goals { get; set; } = string.Empty;

    public List<string> Traits { get; set; } = [];
}
