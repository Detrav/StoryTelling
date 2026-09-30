using StoryTelling.Application.Llm;

namespace StoryTelling.Application.Tools;

public sealed record ToolAgentOutcome(IReadOnlyList<LlmMessage> Messages, int ToolCalls);
