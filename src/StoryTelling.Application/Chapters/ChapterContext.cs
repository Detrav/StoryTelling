using StoryTelling.Application.Llm;

namespace StoryTelling.Application.Chapters;

public sealed record ChapterContext(IReadOnlyList<LlmMessage> Messages, int EstimatedTokens);
