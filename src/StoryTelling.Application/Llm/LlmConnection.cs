namespace StoryTelling.Application.Llm;

public sealed record LlmConnection(string BaseUrl, string ApiKey, TimeSpan Timeout)
{
    public static LlmConnection From(string baseUrl, string apiKey, int timeoutSeconds) =>
        new(baseUrl, apiKey, timeoutSeconds > 0 ? TimeSpan.FromSeconds(timeoutSeconds) : System.Threading.Timeout.InfiniteTimeSpan);
}
