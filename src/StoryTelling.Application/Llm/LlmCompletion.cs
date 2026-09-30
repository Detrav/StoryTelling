namespace StoryTelling.Application.Llm;

public sealed record LlmCompletion(
    string Content,
    string FinishReason,
    int? PromptTokens,
    int? CompletionTokens);
