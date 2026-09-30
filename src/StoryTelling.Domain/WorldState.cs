namespace StoryTelling.Domain;

public sealed class WorldState
{
    public string TimeAndPlace { get; set; } = string.Empty;

    public List<CharacterState> Characters { get; set; } = [];

    public List<string> Locations { get; set; } = [];

    public List<string> Items { get; set; } = [];

    public List<string> ActiveThreads { get; set; } = [];

    public List<string> ResolvedThreads { get; set; } = [];

    public List<string> RecentEvents { get; set; } = [];

    public List<string> OpenQuestions { get; set; } = [];
}
