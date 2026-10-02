using StoryTelling.Domain;

namespace StoryTelling.Application.Generation;

public sealed record GenerationRequest
{
    public required GenerationTarget Target { get; init; }

    public required GenerationContext Context { get; init; }

    public string Brief { get; init; } = string.Empty;

    public int Variants { get; init; } = 3;

    public Project? Snapshot { get; init; }

    public bool Bundle { get; init; }

    public IReadOnlyList<string> Avoid { get; init; } = [];
}
