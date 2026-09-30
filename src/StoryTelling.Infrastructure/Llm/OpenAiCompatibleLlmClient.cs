using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;
using StoryTelling.Application.Abstractions;
using StoryTelling.Application.Llm;
using StoryTelling.Infrastructure.Json;

namespace StoryTelling.Infrastructure.Llm;

public sealed class OpenAiCompatibleLlmClient : ILlmClient
{
    private const int MaxStructuredAttempts = 3;

    private readonly HttpClient _httpClient;

    public OpenAiCompatibleLlmClient(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<LlmCompletion> CompleteAsync(
        LlmConnection connection,
        LlmRequest request,
        CancellationToken cancellationToken = default)
    {
        var payload = BuildPayload(request, stream: false);
        using var cts = CreateRequestCts(connection, cancellationToken);
        using var response = await SendAsync(connection, payload, cts.Token, cancellationToken).ConfigureAwait(false);
        var body = await ReadBodyAsync(response, cts.Token, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw CreateHttpException(response.StatusCode, body);
        }

        var parsed = ParseResponse(body);
        var choice = parsed.Choices is { Count: > 0 } choices ? choices[0] : null;
        var content = choice?.Message?.Content ?? string.Empty;
        return new LlmCompletion(content, choice?.FinishReason ?? string.Empty, parsed.Usage?.PromptTokens, parsed.Usage?.CompletionTokens);
    }

    public async IAsyncEnumerable<string> StreamAsync(
        LlmConnection connection,
        LlmRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var payload = BuildPayload(request, stream: true);
        using var cts = CreateRequestCts(connection, cancellationToken);
        using var response = await SendAsync(connection, payload, cts.Token, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await ReadBodyAsync(response, cts.Token, cancellationToken).ConfigureAwait(false);
            throw CreateHttpException(response.StatusCode, errorBody);
        }

        using var stream = await OpenStreamAsync(response, cts.Token, cancellationToken).ConfigureAwait(false);
        using var reader = new StreamReader(stream);

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
                yield return delta;
            }
        }
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
            var content = parsed.Choices is { Count: > 0 } choices ? choices[0].Message?.Content : null;
            if (!string.IsNullOrWhiteSpace(content))
            {
                return content;
            }

            failure = new LlmException(LlmErrorKind.InvalidResponse, "The provider returned empty structured output.");
        }

        throw failure ?? new LlmException(LlmErrorKind.InvalidResponse, "Structured generation failed.");
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
            var content = parsed.Choices is { Count: > 0 } choices ? choices[0].Message?.Content : null;
            if (string.IsNullOrWhiteSpace(content))
            {
                failure = new LlmException(LlmErrorKind.InvalidResponse, "The provider returned empty structured output.");
                continue;
            }

            try
            {
                var value = JsonSerializer.Deserialize(content, typeInfo);
                if (value is not null)
                {
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

    private static async Task<string> ReadBodyAsync(HttpResponseMessage response, CancellationToken token, CancellationToken outer)
    {
        try
        {
            return await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
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
