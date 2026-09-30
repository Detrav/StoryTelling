namespace StoryTelling.Application.Generation;

public sealed record GenerationContext
{
    public IReadOnlyDictionary<string, string> Fields { get; init; } = new Dictionary<string, string>();

    public IReadOnlyList<string> Cast { get; init; } = [];
}
