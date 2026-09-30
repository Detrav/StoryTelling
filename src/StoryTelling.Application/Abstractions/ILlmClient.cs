using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using StoryTelling.Application.Llm;

namespace StoryTelling.Application.Abstractions;

public interface ILlmClient
{
    Task<LlmCompletion> CompleteAsync(
        LlmConnection connection,
        LlmRequest request,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> StreamAsync(
        LlmConnection connection,
        LlmRequest request,
        CancellationToken cancellationToken = default);

    Task<string> CompleteJsonAsync(
        LlmConnection connection,
        LlmRequest request,
        string schemaName,
        JsonNode schema,
        CancellationToken cancellationToken = default);

    Task<T> CompleteStructuredAsync<T>(
        LlmConnection connection,
        LlmRequest request,
        JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken = default);

    Task<LlmStructuredSupport> CheckStructuredOutputAsync(
        LlmConnection connection,
        string model,
        CancellationToken cancellationToken = default);
}
