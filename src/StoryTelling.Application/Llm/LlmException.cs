namespace StoryTelling.Application.Llm;

public sealed class LlmException : Exception
{
    public LlmException(LlmErrorKind kind, string message, int? statusCode = null, Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
        StatusCode = statusCode;
    }

    public LlmErrorKind Kind { get; }

    public int? StatusCode { get; }
}
