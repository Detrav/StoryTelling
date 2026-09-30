namespace StoryTelling.Application.Llm;

public sealed record LlmToolResponse(string Content, string FinishReason, IReadOnlyList<LlmToolCall> ToolCalls);
