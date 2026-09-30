using StoryTelling.Application.Llm;

namespace StoryTelling.Application.Generation;

public sealed class GenerationSession
{
    public string Brief { get; set; } = string.Empty;

    public IReadOnlyList<LlmMessage> Messages { get; set; } = [];

    public int ToolCalls { get; set; }

    public bool Gathered { get; set; }
}
