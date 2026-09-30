using StoryTelling.Application.Abstractions;
using StoryTelling.Application.Generation;
using StoryTelling.Application.Llm;

namespace StoryTelling.Application.Tools;

public sealed class ToolAgent
{
    public const int DefaultMaxResultChars = 24000;

    private readonly ILlmClient _llmClient;

    public ToolAgent(ILlmClient llmClient) => _llmClient = llmClient;

    public async Task<ToolAgentOutcome> GatherAsync(
        LlmConnection connection,
        LlmRequest request,
        IReadOnlyList<LlmTool> tools,
        Func<string, string, string> invoke,
        int maxCalls,
        int maxResultChars = DefaultMaxResultChars,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var messages = new List<LlmMessage>(request.Messages);
        var cache = new Dictionary<string, string>(StringComparer.Ordinal);
        var calls = 0;
        var usedChars = 0;

        while (calls < maxCalls)
        {
            var response = await _llmClient
                .CompleteWithToolsAsync(connection, request with { Messages = messages }, tools, cancellationToken)
                .ConfigureAwait(false);

            if (response.ToolCalls.Count == 0)
            {
                break;
            }

            messages.Add(LlmMessage.AssistantToolCalls(response.Content, response.ToolCalls));

            foreach (var call in response.ToolCalls)
            {
                if (calls >= maxCalls)
                {
                    break;
                }

                var key = $"{call.Name}\u0000{call.Arguments}";
                if (!cache.TryGetValue(key, out var result))
                {
                    result = InvokeTool(invoke, call);
                    cache[key] = result;
                }

                result = Bound(result, maxResultChars - usedChars);
                usedChars += result.Length;
                messages.Add(LlmMessage.Tool(call.Id, result));
                calls++;
            }

            progress?.Report(new GenerationProgress("Gathering context", calls));
        }

        return new ToolAgentOutcome(messages, calls);
    }

    private static string InvokeTool(Func<string, string, string> invoke, LlmToolCall call)
    {
        try
        {
            return invoke(call.Name, call.Arguments);
        }
        catch (Exception exception)
        {
            return $"Tool '{call.Name}' failed: {exception.Message}";
        }
    }

    private static string Bound(string result, int remaining)
    {
        if (remaining <= 0)
        {
            return "[context budget exhausted; no further tool output]";
        }

        return result.Length <= remaining ? result : result[..remaining] + "…[truncated]";
    }
}
