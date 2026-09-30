namespace StoryTelling.Application.Llm;

public enum LlmErrorKind
{
    Authentication,
    RateLimited,
    Timeout,
    InvalidResponse,
    InvalidRequest,
    Network,
    Unknown,
}
