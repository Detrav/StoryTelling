namespace StoryTelling.Application.Llm;

public sealed record LlmMessage(LlmRole Role, string Content)
{
    public static LlmMessage System(string content) => new(LlmRole.System, content);

    public static LlmMessage User(string content) => new(LlmRole.User, content);

    public static LlmMessage Assistant(string content) => new(LlmRole.Assistant, content);
}
