namespace StoryTelling.Application.Generation;

public sealed record GenerationContext
{
    public string ProjectName { get; init; } = string.Empty;

    public string WorldTitle { get; init; } = string.Empty;

    public string WorldBody { get; init; } = string.Empty;

    public string Genre { get; init; } = string.Empty;

    public string Tone { get; init; } = string.Empty;

    public string Premise { get; init; } = string.Empty;

    public string Direction { get; init; } = string.Empty;
}
