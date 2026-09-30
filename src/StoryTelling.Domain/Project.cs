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

    public PlotDescription Plot { get; set; } = new();

    public List<ExtraFile> ExtraFiles { get; set; } = [];

    public List<OutlineEntry> Outline { get; set; } = [];

    public List<Chapter> Chapters { get; set; } = [];

    public WorldState WorldState { get; set; } = new();

    public List<AssistantMessage> Transcript { get; set; } = [];
}
