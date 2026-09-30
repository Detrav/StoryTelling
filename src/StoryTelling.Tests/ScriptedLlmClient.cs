using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using StoryTelling.Application.Abstractions;
using StoryTelling.Application.Llm;

namespace StoryTelling.Tests;

internal sealed class ScriptedLlmClient : ILlmClient
{
    private readonly Queue<LlmToolResponse> _toolResponses;

    public ScriptedLlmClient(string jsonResponse, IEnumerable<LlmToolResponse> toolResponses)
    {
        JsonResponse = jsonResponse;
        _toolResponses = new Queue<LlmToolResponse>(toolResponses);
    }

    public string JsonResponse { get; set; }

    public List<LlmRequest> ToolRequests { get; } = [];

    public List<LlmRequest> JsonRequests { get; } = [];

    public LlmRequest? LastJsonRequest => JsonRequests.LastOrDefault();

    public Task<LlmCompletion> CompleteAsync(
        LlmConnection connection,
        LlmRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public async IAsyncEnumerable<string> StreamAsync(
        LlmConnection connection,
        LlmRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        yield break;
    }

    public Task<string> CompleteJsonAsync(
        LlmConnection connection,
        LlmRequest request,
        string schemaName,
        JsonNode schema,
        CancellationToken cancellationToken = default)
    {
        JsonRequests.Add(request);
        return Task.FromResult(JsonResponse);
    }

    public Task<T> CompleteStructuredAsync<T>(
        LlmConnection connection,
        LlmRequest request,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<LlmToolResponse> CompleteWithToolsAsync(
        LlmConnection connection,
        LlmRequest request,
        IReadOnlyList<LlmTool> tools,
        CancellationToken cancellationToken = default)
    {
        ToolRequests.Add(request);
        var response = _toolResponses.Count > 0
            ? _toolResponses.Dequeue()
            : new LlmToolResponse(string.Empty, "stop", []);
        return Task.FromResult(response);
    }

    public Task<LlmStructuredSupport> CheckStructuredOutputAsync(
        LlmConnection connection,
        string model,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new LlmStructuredSupport(true, null));
}
