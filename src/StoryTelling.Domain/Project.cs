namespace StoryTelling.Domain;

public sealed class Project
{
    public int SchemaVersion { get; set; } = ProjectSchema.Version;

    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset UpdatedUtc { get; set; }

    public StorySettings Settings { get; set; } = new();

    public WorldLore Lore { get; set; } = new();

    public List<Character> Characters { get; set; } = [];

    public StoryFrame Frame { get; set; } = new();

    public List<KnowledgeEntry> Knowledge { get; set; } = [];

    public List<Chapter> Chapters { get; set; } = [];

    public WorldState WorldState { get; set; } = new();
}
