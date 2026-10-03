using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging;
using StoryTelling.Application.Abstractions;
using StoryTelling.Application.Llm;
using StoryTelling.Infrastructure.Json;

namespace StoryTelling.Infrastructure.Llm;

public sealed class OpenAiCompatibleLlmClient : ILlmClient
{
    private const int MaxStructuredAttempts = 3;

    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAiCompatibleLlmClient>? _logger;

    public OpenAiCompatibleLlmClient(HttpClient httpClient, ILogger<OpenAiCompatibleLlmClient>? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<LlmCompletion> CompleteAsync(
        LlmConnection connection,
        LlmRequest request,
        CancellationToken cancellationToken = default)
    {
        var payload = BuildPayload(request, stream: false);
        using var cts = CreateRequestCts(connection, cancellationToken);
        var started = Stopwatch.GetTimestamp();
        using var response = await SendAsync(connection, payload, cts.Token, cancellationToken).ConfigureAwait(false);
        var body = await ReadBodyAsync(response, cts.Token, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw CreateHttpException(response.StatusCode, body);
        }

        var parsed = ParseResponse(body);
        var choice = parsed.Choices is { Count: > 0 } choices ? choices[0] : null;
        var content = choice?.Message?.Content ?? string.Empty;
        LogCompletion("complete", payload.Model, content, choice?.FinishReason, parsed.Usage, started);
        return new LlmCompletion(content, choice?.FinishReason ?? string.Empty, parsed.Usage?.PromptTokens, parsed.Usage?.CompletionTokens);
    }

    public async IAsyncEnumerable<string> StreamAsync(
        LlmConnection connection,
        LlmRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var payload = BuildPayload(request, stream: true);
        using var cts = CreateRequestCts(connection, cancellationToken);
        var started = Stopwatch.GetTimestamp();
        using var response = await SendAsync(connection, payload, cts.Token, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await ReadBodyAsync(response, cts.Token, cancellationToken).ConfigureAwait(false);
            throw CreateHttpException(response.StatusCode, errorBody);
        }

        using var stream = await OpenStreamAsync(response, cts.Token, cancellationToken).ConfigureAwait(false);
        using var reader = new StreamReader(stream);
        var collected = new StringBuilder();

        while (true)
        {
            var line = await ReadLineAsync(reader, cts.Token, cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                break;
            }

            if (line.Length == 0 || line[0] == ':' || !line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            var data = line[5..].Trim();
            if (data.Length == 0)
            {
                continue;
            }

            if (data == "[DONE]")
            {
                break;
            }

            var delta = ExtractDelta(data);
            if (delta.Length > 0)
            {
                collected.Append(delta);
                yield return delta;
            }
        }

        LogCompletion("stream", payload.Model, collected.ToString(), "stop", null, started);
    }

    public async Task<string> CompleteJsonAsync(
        LlmConnection connection,
        LlmRequest request,
        string schemaName,
        JsonNode schema,
        CancellationToken cancellationToken = default)
    {
        var responseFormat = new ResponseFormatPayload
        {
            Type = "json_schema",
            JsonSchema = new JsonSchemaPayload { Name = schemaName, Strict = true, Schema = schema },
        };

        LlmException? failure = null;
        for (var attempt = 0; attempt < MaxStructuredAttempts; attempt++)
        {
            var payload = BuildPayload(request, stream: false, responseFormat);
            using var cts = CreateRequestCts(connection, cancellationToken);
            using var response = await SendAsync(connection, payload, cts.Token, cancellationToken).ConfigureAwait(false);
            var body = await ReadBodyAsync(response, cts.Token, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw CreateHttpException(response.StatusCode, body);
            }

            var parsed = ParseResponse(body);
            var choice = parsed.Choices is { Count: > 0 } choices ? choices[0] : null;
            var content = choice?.Message?.Content;
            if (!string.IsNullOrWhiteSpace(content))
            {
                LogCompletion($"structured {schemaName}", payload.Model, content, choice?.FinishReason, parsed.Usage, null);
                return content;
            }

            _logger?.LogWarning("LLM structured {Schema} returned empty content (attempt {Attempt}/{Max})", schemaName, attempt + 1, MaxStructuredAttempts);
            failure = new LlmException(LlmErrorKind.InvalidResponse, "The provider returned empty structured output.");
        }

        throw failure ?? new LlmException(LlmErrorKind.InvalidResponse, "Structured generation failed.");
    }

    public async Task<LlmToolResponse> CompleteWithToolsAsync(
        LlmConnection connection,
        LlmRequest request,
        IReadOnlyList<LlmTool> tools,
        CancellationToken cancellationToken = default)
    {
        var payload = BuildPayload(request, stream: false);
        payload.Tools =
        [
            .. tools.Select(tool => new ChatToolPayload
            {
                Function = new ChatFunctionPayload
                {
                    Name = tool.Name,
                    Description = tool.Description,
                    Parameters = tool.Parameters,
                },
            }),
        ];
        payload.ToolChoice = "auto";

        using var cts = CreateRequestCts(connection, cancellationToken);
        using var response = await SendAsync(connection, payload, cts.Token, cancellationToken).ConfigureAwait(false);
        var body = await ReadBodyAsync(response, cts.Token, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw CreateHttpException(response.StatusCode, body);
        }

        var parsed = ParseResponse(body);
        var choice = parsed.Choices is { Count: > 0 } choices ? choices[0] : null;
        var message = choice?.Message;

        IReadOnlyList<LlmToolCall> toolCalls = message?.ToolCalls is { Count: > 0 } calls
            ? [.. calls.Select(call => new LlmToolCall(
                call.Id ?? string.Empty,
                call.Function?.Name ?? string.Empty,
                string.IsNullOrWhiteSpace(call.Function?.Arguments) ? "{}" : call.Function!.Arguments!))]
            : [];

        return new LlmToolResponse(message?.Content ?? string.Empty, choice?.FinishReason ?? string.Empty, toolCalls);
    }

    public async Task<T> CompleteStructuredAsync<T>(
        LlmConnection connection,
        LlmRequest request,
        JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken = default)
    {
        var responseFormat = new ResponseFormatPayload
        {
            Type = "json_schema",
            JsonSchema = new JsonSchemaPayload
            {
                Name = typeInfo.Type.Name,
                Strict = true,
                Schema = JsonSchemaExporter.GetJsonSchemaAsNode(typeInfo.Options, typeInfo.Type),
            },
        };

        LlmException? failure = null;
        for (var attempt = 0; attempt < MaxStructuredAttempts; attempt++)
        {
            var payload = BuildPayload(request, stream: false, responseFormat);
            using var cts = CreateRequestCts(connection, cancellationToken);
            using var response = await SendAsync(connection, payload, cts.Token, cancellationToken).ConfigureAwait(false);
            var body = await ReadBodyAsync(response, cts.Token, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw CreateHttpException(response.StatusCode, body);
            }

            var parsed = ParseResponse(body);
            var choice = parsed.Choices is { Count: > 0 } choices ? choices[0] : null;
            var content = choice?.Message?.Content;
            if (string.IsNullOrWhiteSpace(content))
            {
                _logger?.LogWarning("LLM structured {Schema} returned empty content (attempt {Attempt}/{Max})", typeInfo.Type.Name, attempt + 1, MaxStructuredAttempts);
                failure = new LlmException(LlmErrorKind.InvalidResponse, "The provider returned empty structured output.");
                continue;
            }

            try
            {
                var value = JsonSerializer.Deserialize(content, typeInfo);
                if (value is not null)
                {
                    LogCompletion($"structured {typeInfo.Type.Name}", payload.Model, content, choice?.FinishReason, parsed.Usage, null);
                    return value;
                }

                failure = new LlmException(LlmErrorKind.InvalidResponse, "The provider returned an empty JSON value.");
            }
            catch (JsonException exception)
            {
                failure = new LlmException(LlmErrorKind.InvalidResponse, "The provider returned JSON that does not match the schema.", null, exception);
            }
        }

        throw failure ?? new LlmException(LlmErrorKind.InvalidResponse, "Structured generation failed.");
    }

    public async Task<LlmStructuredSupport> CheckStructuredOutputAsync(
        LlmConnection connection,
        string model,
        CancellationToken cancellationToken = default)
    {
        var schema = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["ok"] = new JsonObject { ["type"] = "boolean" },
            },
            ["required"] = new JsonArray { "ok" },
            ["additionalProperties"] = false,
        };

        var responseFormat = new ResponseFormatPayload
        {
            Type = "json_schema",
            JsonSchema = new JsonSchemaPayload { Name = "probe", Strict = true, Schema = schema },
        };

        var request = new LlmRequest
        {
            Model = model,
            Messages = [LlmMessage.User("Return ok=true.")],
            Temperature = 0,
            MaxTokens = 32,
        };

        try
        {
            var payload = BuildPayload(request, stream: false, responseFormat);
            using var cts = CreateRequestCts(connection, cancellationToken);
            using var response = await SendAsync(connection, payload, cts.Token, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return new LlmStructuredSupport(true, null);
            }

            var body = await ReadBodyAsync(response, cts.Token, cancellationToken).ConfigureAwait(false);
            return new LlmStructuredSupport(false, ExtractErrorMessage(body) ?? $"HTTP {(int)response.StatusCode}");
        }
        catch (LlmException exception)
        {
            return new LlmStructuredSupport(false, exception.Message);
        }
    }

    private void LogCompletion(string kind, string model, string content, string? finishReason, ChatUsagePayload? usage, long? started)
    {
        if (_logger?.IsEnabled(LogLevel.Debug) != true)
        {
            return;
        }

        var elapsed = started is { } value ? (int)Stopwatch.GetElapsedTime(value).TotalMilliseconds : -1;
        _logger.LogDebug(
            "LLM {Kind} {Model} finished in {ElapsedMs} ms (finish: {FinishReason}, prompt {PromptTokens}, completion {CompletionTokens}, {Chars} chars)\n{Content}",
            kind,
            model,
            elapsed,
            finishReason ?? "unknown",
            usage?.PromptTokens,
            usage?.CompletionTokens,
            content.Length,
            content);
    }

    internal static string BuildChatCompletionsUrl(string baseUrl)
    {
        var trimmed = (baseUrl ?? string.Empty).Trim().TrimEnd('/');
        if (trimmed.Length == 0)
        {
            throw new LlmException(LlmErrorKind.InvalidRequest, "A base URL is required.");
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            throw new LlmException(LlmErrorKind.InvalidRequest, $"The base URL is not valid: {baseUrl}");
        }

        if (uri.AbsolutePath is "" or "/")
        {
            trimmed += "/v1";
        }

        return trimmed + "/chat/completions";
    }

    private static ChatCompletionRequestPayload BuildPayload(LlmRequest request, bool stream, ResponseFormatPayload? responseFormat = null)
    {
        if (string.IsNullOrWhiteSpace(request.Model))
        {
            throw new LlmException(LlmErrorKind.InvalidRequest, "A model name is required.");
        }

        if (request.Messages.Count == 0)
        {
            throw new LlmException(LlmErrorKind.InvalidRequest, "At least one message is required.");
        }

        var payload = new ChatCompletionRequestPayload
        {
            Model = request.Model,
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens,
            Stream = stream,
            Messages = [.. request.Messages.Select(message => new ChatMessagePayload
            {
                Role = RoleName(message.Role),
                Content = message.Content,
                ToolCallId = message.ToolCallId,
                ToolCalls = message.ToolCalls.Count == 0
                    ? null
                    : [.. message.ToolCalls.Select(call => new ChatToolCallPayload
                    {
                        Id = call.Id,
                        Type = "function",
                        Function = new ChatFunctionCallPayload { Name = call.Name, Arguments = call.Arguments },
                    })],
            })],
        };

        if (responseFormat is not null)
        {
            payload.ResponseFormat = responseFormat;
        }
        else if (request.JsonMode)
        {
            payload.ResponseFormat = new ResponseFormatPayload();
        }

        return payload;
    }

    private static string RoleName(LlmRole role) => role switch
    {
        LlmRole.System => "system",
        LlmRole.Assistant => "assistant",
        LlmRole.Tool => "tool",
        _ => "user",
    };

    private static CancellationTokenSource CreateRequestCts(LlmConnection connection, CancellationToken cancellationToken)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (connection.Timeout > TimeSpan.Zero)
        {
            cts.CancelAfter(connection.Timeout);
        }

        return cts;
    }

    private async Task<HttpResponseMessage> SendAsync(
        LlmConnection connection,
        ChatCompletionRequestPayload payload,
        CancellationToken token,
        CancellationToken outer)
    {
        var url = BuildChatCompletionsUrl(connection.BaseUrl);
        if (_logger?.IsEnabled(LogLevel.Debug) == true)
        {
            _logger.LogDebug("LLM request POST {Url} ({Model}, temperature {Temperature}, max tokens {MaxTokens})\n{Payload}",
                url,
                payload.Model,
                payload.Temperature,
                payload.MaxTokens,
                JsonSerializer.Serialize(payload, LlmJsonContext.Default.ChatCompletionRequestPayload));
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload, LlmJsonContext.Default.ChatCompletionRequestPayload),
        };

        if (!string.IsNullOrWhiteSpace(connection.ApiKey))
        {
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", connection.ApiKey);
        }

        try
        {
            return await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!outer.IsCancellationRequested)
        {
            throw new LlmException(LlmErrorKind.Timeout, "The provider request timed out.");
        }
        catch (HttpRequestException exception)
        {
            throw new LlmException(LlmErrorKind.Network, exception.Message, null, exception);
        }
    }

    private async Task<string> ReadBodyAsync(HttpResponseMessage response, CancellationToken token, CancellationToken outer)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("LLM response {StatusCode} {Reason} ({Bytes} bytes)\n{Body}", (int)response.StatusCode, response.ReasonPhrase, body.Length, body);
            }
            else if (_logger?.IsEnabled(LogLevel.Debug) == true)
            {
                _logger.LogDebug("LLM response {StatusCode} ({Bytes} bytes)\n{Body}", (int)response.StatusCode, body.Length, body);
            }

            return body;
        }
        catch (OperationCanceledException) when (!outer.IsCancellationRequested)
        {
            throw new LlmException(LlmErrorKind.Timeout, "The provider request timed out.");
        }
    }

    private static async Task<Stream> OpenStreamAsync(HttpResponseMessage response, CancellationToken token, CancellationToken outer)
    {
        try
        {
            return await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!outer.IsCancellationRequested)
        {
            throw new LlmException(LlmErrorKind.Timeout, "The provider request timed out.");
        }
    }

    private static async Task<string?> ReadLineAsync(StreamReader reader, CancellationToken token, CancellationToken outer)
    {
        try
        {
            return await reader.ReadLineAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!outer.IsCancellationRequested)
        {
            throw new LlmException(LlmErrorKind.Timeout, "The provider request timed out.");
        }
    }

    private static ChatCompletionResponsePayload ParseResponse(string body)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize(body, LlmJsonContext.Default.ChatCompletionResponsePayload);
            if (parsed is null)
            {
                throw new LlmException(LlmErrorKind.InvalidResponse, "The provider returned an empty response.");
            }

            if (parsed.Error is not null)
            {
                throw new LlmException(LlmErrorKind.InvalidResponse, parsed.Error.Message ?? "The provider returned an error.");
            }

            return parsed;
        }
        catch (JsonException exception)
        {
            throw new LlmException(LlmErrorKind.InvalidResponse, "The provider returned malformed JSON.", null, exception);
        }
    }

    private static string ExtractDelta(string data)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize(data, LlmJsonContext.Default.ChatCompletionResponsePayload);
            return parsed?.Choices is { Count: > 0 } choices ? choices[0].Delta?.Content ?? string.Empty : string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static LlmException CreateHttpException(HttpStatusCode statusCode, string body)
    {
        var kind = statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => LlmErrorKind.Authentication,
            HttpStatusCode.TooManyRequests => LlmErrorKind.RateLimited,
            HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout => LlmErrorKind.Timeout,
            HttpStatusCode.BadRequest or HttpStatusCode.NotFound or HttpStatusCode.UnprocessableEntity => LlmErrorKind.InvalidRequest,
            _ => LlmErrorKind.Unknown,
        };

        var message = ExtractErrorMessage(body) ?? $"The provider returned HTTP {(int)statusCode}.";
        return new LlmException(kind, message, (int)statusCode);
    }

    private static string? ExtractErrorMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.String)
                {
                    return error.GetString();
                }

                if (error.ValueKind == JsonValueKind.Object
                    && error.TryGetProperty("message", out var message)
                    && message.ValueKind == JsonValueKind.String)
                {
                    return message.GetString();
                }
            }

            if (root.TryGetProperty("message", out var topLevel) && topLevel.ValueKind == JsonValueKind.String)
            {
                return topLevel.GetString();
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }
}

