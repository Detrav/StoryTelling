namespace StoryTelling.Application.Llm;

public sealed record LlmRequest
{
    public required string Model { get; init; }

    public required IReadOnlyList<LlmMessage> Messages { get; init; }

    public double Temperature { get; init; } = 0.8;

    public string? ReasoningEffort { get; init; }

    public int? MaxTokens { get; init; }

    public bool JsonMode { get; init; }
}
