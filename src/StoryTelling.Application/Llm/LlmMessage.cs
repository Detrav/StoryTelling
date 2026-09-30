namespace StoryTelling.Application.Llm;

public sealed record LlmMessage(LlmRole Role, string Content)
{
    public IReadOnlyList<LlmToolCall> ToolCalls { get; init; } = [];

    public string? ToolCallId { get; init; }

    public static LlmMessage System(string content) => new(LlmRole.System, content);

    public static LlmMessage User(string content) => new(LlmRole.User, content);

    public static LlmMessage Assistant(string content) => new(LlmRole.Assistant, content);

    public static LlmMessage AssistantToolCalls(string content, IReadOnlyList<LlmToolCall> toolCalls) =>
        new(LlmRole.Assistant, content) { ToolCalls = toolCalls };

    public static LlmMessage Tool(string toolCallId, string content) =>
        new(LlmRole.Tool, content) { ToolCallId = toolCallId };
}
