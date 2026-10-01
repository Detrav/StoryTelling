using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using StoryTelling.Application.Abstractions;
using StoryTelling.Application.Llm;

namespace StoryTelling.Tests;

internal sealed class FakeLlmClient : ILlmClient
{
    private readonly string _response;

    public FakeLlmClient(string response) => _response = response;

    public LlmRequest? LastRequest { get; private set; }

    public LlmConnection? LastConnection { get; private set; }

    public string? LastSchemaName { get; private set; }

    public JsonNode? LastSchema { get; private set; }

    public LlmStructuredSupport StructuredSupport { get; set; } = new(true, null);

    public Queue<string> JsonQueue { get; } = new();

    public int JsonCallCount { get; private set; }

    public LlmToolResponse ToolResponse { get; set; } = new(string.Empty, "stop", []);

    public Task<LlmCompletion> CompleteAsync(
        LlmConnection connection,
        LlmRequest request,
        CancellationToken cancellationToken = default)
    {
        LastConnection = connection;
        LastRequest = request;
        return Task.FromResult(new LlmCompletion(_response, "stop", 1, 1));
    }

    public async IAsyncEnumerable<string> StreamAsync(
        LlmConnection connection,
        LlmRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        yield return _response;
    }

    public Task<string> CompleteJsonAsync(
        LlmConnection connection,
        LlmRequest request,
        string schemaName,
        JsonNode schema,
        CancellationToken cancellationToken = default)
    {
        LastConnection = connection;
        LastRequest = request;
        LastSchemaName = schemaName;
        LastSchema = schema;
        JsonCallCount++;
        return Task.FromResult(JsonQueue.Count > 0 ? JsonQueue.Dequeue() : _response);
    }

    public Task<T> CompleteStructuredAsync<T>(
        LlmConnection connection,
        LlmRequest request,
        JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken = default)
    {
        LastConnection = connection;
        LastRequest = request;
        return Task.FromResult(JsonSerializer.Deserialize(_response, typeInfo)!);
    }

    public Task<LlmStructuredSupport> CheckStructuredOutputAsync(
        LlmConnection connection,
        string model,
        CancellationToken cancellationToken = default)
    {
        LastConnection = connection;
        return Task.FromResult(StructuredSupport);
    }

    public Task<LlmToolResponse> CompleteWithToolsAsync(
        LlmConnection connection,
        LlmRequest request,
        IReadOnlyList<LlmTool> tools,
        CancellationToken cancellationToken = default)
    {
        LastConnection = connection;
        LastRequest = request;
        return Task.FromResult(ToolResponse);
    }
}
