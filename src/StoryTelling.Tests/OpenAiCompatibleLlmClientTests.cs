using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using StoryTelling.Application.Llm;
using StoryTelling.Infrastructure.Llm;

namespace StoryTelling.Tests;

public sealed class OpenAiCompatibleLlmClientTests
{
    private static readonly LlmConnection _connection = new("http://127.0.0.1:1234", "secret", TimeSpan.FromSeconds(30));

    [Fact]
    public async Task CompleteAsync_ParsesContentAndUsage()
    {
        const string body = """
        {"choices":[{"message":{"role":"assistant","content":"Hello"},"finish_reason":"stop"}],"usage":{"prompt_tokens":5,"completion_tokens":2}}
        """;
        var (client, _) = Create((_, _) => Task.FromResult(Json(HttpStatusCode.OK, body)));

        var completion = await client.CompleteAsync(_connection, Request());

        Assert.Equal("Hello", completion.Content);
        Assert.Equal("stop", completion.FinishReason);
        Assert.Equal(5, completion.PromptTokens);
        Assert.Equal(2, completion.CompletionTokens);
    }

    [Fact]
    public async Task CompleteAsync_AddsV1ToBareHost_AndSendsBearerToken()
    {
        HttpRequestMessage? captured = null;
        var (client, _) = Create((request, _) =>
        {
            captured = request;
            return Task.FromResult(Json(HttpStatusCode.OK, """{"choices":[{"message":{"content":"ok"}}]}"""));
        });

        await client.CompleteAsync(_connection, Request());

        Assert.NotNull(captured);
        Assert.Equal("http://127.0.0.1:1234/v1/chat/completions", captured!.RequestUri!.ToString());
        Assert.Equal("Bearer", captured.Headers.Authorization!.Scheme);
        Assert.Equal("secret", captured.Headers.Authorization!.Parameter);
    }

    [Fact]
    public async Task CompleteAsync_KeepsV1Path_WhenProvided()
    {
        HttpRequestMessage? captured = null;
        var (client, _) = Create((request, _) =>
        {
            captured = request;
            return Task.FromResult(Json(HttpStatusCode.OK, """{"choices":[{"message":{"content":"ok"}}]}"""));
        });

        await client.CompleteAsync(new LlmConnection("http://127.0.0.1:1234/v1", "", TimeSpan.FromSeconds(30)), Request());

        Assert.Equal("http://127.0.0.1:1234/v1/chat/completions", captured!.RequestUri!.ToString());
    }

    [Fact]
    public async Task CompleteAsync_Unauthorized_ThrowsAuthentication()
    {
        var (client, _) = Create((_, _) => Task.FromResult(Json(HttpStatusCode.Unauthorized, """{"error":{"message":"bad key"}}""")));

        var exception = await Assert.ThrowsAsync<LlmException>(() => client.CompleteAsync(_connection, Request()));

        Assert.Equal(LlmErrorKind.Authentication, exception.Kind);
        Assert.Equal(401, exception.StatusCode);
        Assert.Equal("bad key", exception.Message);
    }

    [Fact]
    public async Task CompleteAsync_TooManyRequests_ThrowsRateLimited()
    {
        var (client, _) = Create((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)));

        var exception = await Assert.ThrowsAsync<LlmException>(() => client.CompleteAsync(_connection, Request()));

        Assert.Equal(LlmErrorKind.RateLimited, exception.Kind);
    }

    [Fact]
    public async Task CompleteAsync_MalformedJson_ThrowsInvalidResponse()
    {
        var (client, _) = Create((_, _) => Task.FromResult(Json(HttpStatusCode.OK, "{ not json")));

        var exception = await Assert.ThrowsAsync<LlmException>(() => client.CompleteAsync(_connection, Request()));

        Assert.Equal(LlmErrorKind.InvalidResponse, exception.Kind);
    }

    [Fact]
    public async Task CompleteAsync_Timeout_ThrowsTimeout()
    {
        var (client, _) = Create(async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var exception = await Assert.ThrowsAsync<LlmException>(() => client.CompleteAsync(
            new LlmConnection("http://127.0.0.1:1234/v1", string.Empty, TimeSpan.FromMilliseconds(50)),
            Request()));

        Assert.Equal(LlmErrorKind.Timeout, exception.Kind);
    }

    [Fact]
    public async Task CompleteAsync_WithoutModel_ThrowsInvalidRequest()
    {
        var (client, _) = Create((_, _) => Task.FromResult(Json(HttpStatusCode.OK, "{}")));

        var exception = await Assert.ThrowsAsync<LlmException>(() => client.CompleteAsync(
            _connection,
            new LlmRequest { Model = " ", Messages = [LlmMessage.User("hi")] }));

        Assert.Equal(LlmErrorKind.InvalidRequest, exception.Kind);
    }

    [Fact]
    public async Task CompleteAsync_NetworkFailure_ThrowsNetwork()
    {
        var (client, _) = Create((_, _) => throw new HttpRequestException("connection refused"));

        var exception = await Assert.ThrowsAsync<LlmException>(() => client.CompleteAsync(_connection, Request()));

        Assert.Equal(LlmErrorKind.Network, exception.Kind);
    }

    [Fact]
    public async Task StreamAsync_YieldsDeltasAndStopsAtDone()
    {
        const string body =
            "data: {\"choices\":[{\"delta\":{\"content\":\"Hel\"}}]}\n\n" +
            "data: {\"choices\":[{\"delta\":{\"content\":\"lo\"}}]}\n\n" +
            "data: [DONE]\n\n";
        var (client, _) = Create((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "text/event-stream"),
        }));

        var pieces = new List<string>();
        await foreach (var piece in client.StreamAsync(_connection, Request()))
        {
            pieces.Add(piece);
        }

        Assert.Equal(new[] { "Hel", "lo" }, pieces);
    }

    [Fact]
    public async Task CompleteStructuredAsync_SendsJsonSchemaAndDeserializes()
    {
        var (client, handler) = Create((_, _) => Task.FromResult(Chunk("{\"answer\":\"hi\"}")));

        var result = await client.CompleteStructuredAsync(_connection, Request(), TestJsonContext.Default.ProbeModel);

        Assert.Equal("hi", result.Answer);
        Assert.Contains("\"json_schema\"", handler.LastRequestBody);
        Assert.Contains("\"strict\":true", handler.LastRequestBody);
        Assert.Contains("\"type\":\"json_schema\"", handler.LastRequestBody);
    }

    [Fact]
    public async Task CompleteStructuredAsync_RetriesMalformedResponse()
    {
        var calls = 0;
        var (client, _) = Create((_, _) =>
        {
            calls++;
            return Task.FromResult(Chunk(calls == 1 ? "not json" : "{\"answer\":\"ok\"}"));
        });

        var result = await client.CompleteStructuredAsync(_connection, Request(), TestJsonContext.Default.ProbeModel);

        Assert.Equal("ok", result.Answer);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task CompleteStructuredAsync_GivesUpAfterBoundedAttempts()
    {
        var calls = 0;
        var (client, _) = Create((_, _) =>
        {
            calls++;
            return Task.FromResult(Chunk("still not json"));
        });

        var exception = await Assert.ThrowsAsync<LlmException>(() =>
            client.CompleteStructuredAsync(_connection, Request(), TestJsonContext.Default.ProbeModel));

        Assert.Equal(LlmErrorKind.InvalidResponse, exception.Kind);
        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task CheckStructuredOutputAsync_Supported_ReturnsTrue()
    {
        var (client, _) = Create((_, _) => Task.FromResult(Chunk("{\"ok\":true}")));

        var support = await client.CheckStructuredOutputAsync(_connection, "test-model");

        Assert.True(support.Supported);
    }

    [Fact]
    public async Task CheckStructuredOutputAsync_Unsupported_ReadsStringError()
    {
        var (client, _) = Create((_, _) => Task.FromResult(
            Json(HttpStatusCode.BadRequest, """{"error":"'response_format.type' must be 'json_schema' or 'text'"}""")));

        var support = await client.CheckStructuredOutputAsync(_connection, "test-model");

        Assert.False(support.Supported);
        Assert.Contains("json_schema", support.Detail);
    }

    [Fact]
    public async Task CompleteWithToolsAsync_EncodesToolsAndParsesToolCalls()
    {
        var (client, handler) = Create((_, _) => Task.FromResult(Json(HttpStatusCode.OK,
            """{"choices":[{"finish_reason":"tool_calls","message":{"role":"assistant","content":"","tool_calls":[{"id":"call_1","type":"function","function":{"name":"get_character","arguments":"{\"name\":\"Aria\"}"}}]}}]}""")));

        var response = await client.CompleteWithToolsAsync(
            _connection,
            Request(),
            [new LlmTool("get_character", "Get a character.", new JsonObject { ["type"] = "object" })]);

        Assert.Equal("tool_calls", response.FinishReason);
        var call = Assert.Single(response.ToolCalls);
        Assert.Equal("get_character", call.Name);
        Assert.Equal("{\"name\":\"Aria\"}", call.Arguments);
        Assert.Contains("\"tools\"", handler.LastRequestBody);
        Assert.Contains("\"tool_choice\":\"auto\"", handler.LastRequestBody);
    }

    [Fact]
    public async Task CompleteWithToolsAsync_SerializesAssistantToolCallsAndToolMessages()
    {
        var (client, handler) = Create((_, _) => Task.FromResult(Json(HttpStatusCode.OK,
            """{"choices":[{"finish_reason":"stop","message":{"role":"assistant","content":"done"}}]}""")));
        var request = new LlmRequest
        {
            Model = "m",
            Messages =
            [
                LlmMessage.User("hi"),
                LlmMessage.AssistantToolCalls(string.Empty, [new LlmToolCall("c1", "characters", "{}")]),
                LlmMessage.Tool("c1", "Aria (protagonist)"),
            ],
        };

        await client.CompleteWithToolsAsync(_connection, request, [new LlmTool("characters", "d", new JsonObject())]);

        var body = handler.LastRequestBody!;
        Assert.Contains("\"tool_calls\"", body);
        Assert.Contains("\"tool_call_id\":\"c1\"", body);
        Assert.Contains("\"role\":\"tool\"", body);
        Assert.Contains("\"name\":\"characters\"", body);
    }

    private static LlmRequest Request() => new()
    {
        Model = "test-model",
        Messages = [LlmMessage.User("hi")],
    };

    private static HttpResponseMessage Chunk(string content)
    {
        var escaped = content.Replace("\\", "\\\\").Replace("\"", "\\\"");
        return Json(HttpStatusCode.OK, $"{{\"choices\":[{{\"message\":{{\"content\":\"{escaped}\"}}}}]}}");
    }

    private static (OpenAiCompatibleLlmClient Client, StubHttpMessageHandler Handler) Create(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
    {
        var handler = new StubHttpMessageHandler(responder);
        var httpClient = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        return (new OpenAiCompatibleLlmClient(httpClient), handler);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}
