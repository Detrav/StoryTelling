using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using StoryTelling.Application.Abstractions;
using StoryTelling.Application.Llm;

namespace StoryTelling.Cli;

internal sealed class TracingLlmClient : ILlmClient
{
    private readonly ILlmClient _inner;
    private readonly string _directory;
    private readonly object _gate = new();
    private int _counter;

    public TracingLlmClient(ILlmClient inner, string directory)
    {
        _inner = inner;
        _directory = directory;
        Directory.CreateDirectory(directory);
    }

    public async Task<LlmCompletion> CompleteAsync(LlmConnection connection, LlmRequest request, CancellationToken cancellationToken = default)
    {
        var completion = await _inner.CompleteAsync(connection, request, cancellationToken).ConfigureAwait(false);
        Write("complete", request, new JsonObject
        {
            ["content"] = completion.Content,
            ["finishReason"] = completion.FinishReason,
            ["promptTokens"] = completion.PromptTokens,
            ["completionTokens"] = completion.CompletionTokens,
        });
        return completion;
    }

    public async IAsyncEnumerable<string> StreamAsync(
        LlmConnection connection,
        LlmRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var builder = new StringBuilder();
        await foreach (var delta in _inner.StreamAsync(connection, request, cancellationToken).ConfigureAwait(false))
        {
            builder.Append(delta);
            yield return delta;
        }

        Write("stream", request, new JsonObject { ["content"] = builder.ToString() });
    }

    public async Task<string> CompleteJsonAsync(
        LlmConnection connection,
        LlmRequest request,
        string schemaName,
        JsonNode schema,
        CancellationToken cancellationToken = default)
    {
        var content = await _inner.CompleteJsonAsync(connection, request, schemaName, schema, cancellationToken).ConfigureAwait(false);
        Write($"completeJson_{schemaName}", request, new JsonObject
        {
            ["content"] = content,
            ["schema"] = schema.DeepClone(),
        });
        return content;
    }

    public async Task<T> CompleteStructuredAsync<T>(
        LlmConnection connection,
        LlmRequest request,
        JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken = default)
    {
        var value = await _inner.CompleteStructuredAsync(connection, request, typeInfo, cancellationToken).ConfigureAwait(false);
        Write($"completeStructured_{typeInfo.Type.Name}", request, new JsonObject
        {
            ["content"] = JsonSerializer.Serialize(value, typeInfo),
        });
        return value;
    }

    public async Task<LlmToolResponse> CompleteWithToolsAsync(
        LlmConnection connection,
        LlmRequest request,
        IReadOnlyList<LlmTool> tools,
        CancellationToken cancellationToken = default)
    {
        var response = await _inner.CompleteWithToolsAsync(connection, request, tools, cancellationToken).ConfigureAwait(false);
        Write("tools", request, new JsonObject
        {
            ["content"] = response.Content,
            ["finishReason"] = response.FinishReason,
            ["toolCalls"] = ToolCalls(response.ToolCalls),
        }, tools);
        return response;
    }

    public Task<LlmStructuredSupport> CheckStructuredOutputAsync(LlmConnection connection, string model, CancellationToken cancellationToken = default) =>
        _inner.CheckStructuredOutputAsync(connection, model, cancellationToken);

    private void Write(string kind, LlmRequest request, JsonObject response, IReadOnlyList<LlmTool>? tools = null)
    {
        int index;
        lock (_gate)
        {
            index = ++_counter;
        }

        var record = new JsonObject
        {
            ["seq"] = index,
            ["ts"] = DateTimeOffset.UtcNow.ToString("O"),
            ["kind"] = kind,
            ["model"] = request.Model,
            ["temperature"] = request.Temperature,
            ["maxTokens"] = request.MaxTokens,
            ["messages"] = new JsonArray([.. request.Messages.Select(message => (JsonNode)new JsonObject
            {
                ["role"] = message.Role.ToString(),
                ["content"] = message.Content,
                ["toolCallId"] = message.ToolCallId,
                ["toolCalls"] = ToolCalls(message.ToolCalls),
            })]),
            ["response"] = response,
        };

        if (tools is not null)
        {
            record["tools"] = new JsonArray([.. tools.Select(tool => (JsonNode)new JsonObject
            {
                ["name"] = tool.Name,
                ["description"] = tool.Description,
                ["parameters"] = tool.Parameters.DeepClone(),
            })]);
        }

        var path = Path.Combine(_directory, $"{index:D4}-{Sanitize(kind)}.json");
        File.WriteAllText(path, record.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static JsonArray ToolCalls(IReadOnlyList<LlmToolCall> calls) =>
        new([.. calls.Select(call => (JsonNode)new JsonObject
        {
            ["id"] = call.Id,
            ["name"] = call.Name,
            ["arguments"] = call.Arguments,
        })]);

    private static string Sanitize(string value) =>
        string.Concat(value.Select(character => char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '_'));
}
