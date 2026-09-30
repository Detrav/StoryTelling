using System.Text.Json.Nodes;
using StoryTelling.Application.Llm;
using StoryTelling.Application.Tools;

namespace StoryTelling.Tests;

public sealed class ToolAgentTests
{
    private static readonly LlmConnection _connection = new("http://localhost", string.Empty, TimeSpan.FromSeconds(30));

    [Fact]
    public async Task GatherAsync_RunsToolCallsAndCollectsResults()
    {
        var client = new ScriptedLlmClient("[]",
        [
            new LlmToolResponse(string.Empty, "tool_calls", [new LlmToolCall("call_1", "characters", "{}")]),
            new LlmToolResponse("done", "stop", []),
        ]);
        var agent = new ToolAgent(client);
        var request = new LlmRequest { Model = "m", Messages = [LlmMessage.User("hi")] };
        var invoked = new List<string>();

        var outcome = await agent.GatherAsync(
            _connection,
            request,
            [new LlmTool("characters", "d", new JsonObject())],
            (name, _) => { invoked.Add(name); return "Aria (protagonist)"; },
            maxCalls: 5);

        Assert.Equal(1, outcome.ToolCalls);
        Assert.Contains("characters", invoked);
        Assert.Contains(outcome.Messages, message => message.Role == LlmRole.Tool && message.Content == "Aria (protagonist)" && message.ToolCallId == "call_1");
        Assert.Contains(outcome.Messages, message => message.Role == LlmRole.Assistant && message.ToolCalls.Count == 1);
    }

    [Fact]
    public async Task GatherAsync_StopsAtMaxCalls()
    {
        var responses = Enumerable.Repeat(new LlmToolResponse(string.Empty, "tool_calls", [new LlmToolCall("c", "characters", "{}")]), 10);
        var client = new ScriptedLlmClient("[]", responses);
        var agent = new ToolAgent(client);
        var request = new LlmRequest { Model = "m", Messages = [LlmMessage.User("hi")] };

        var outcome = await agent.GatherAsync(
            _connection, request, [new LlmTool("characters", "d", new JsonObject())], (_, _) => "x", maxCalls: 3);

        Assert.Equal(3, outcome.ToolCalls);
    }

    [Fact]
    public async Task GatherAsync_ToolFailure_IsReported()
    {
        var client = new ScriptedLlmClient("[]",
        [
            new LlmToolResponse(string.Empty, "tool_calls", [new LlmToolCall("c1", "character", "{}")]),
            new LlmToolResponse(string.Empty, "stop", []),
        ]);
        var agent = new ToolAgent(client);
        var request = new LlmRequest { Model = "m", Messages = [LlmMessage.User("hi")] };

        var outcome = await agent.GatherAsync(
            _connection,
            request,
            [new LlmTool("character", "d", new JsonObject())],
            (_, _) => throw new InvalidOperationException("missing name"),
            maxCalls: 5);

        Assert.Contains(outcome.Messages, message => message.Role == LlmRole.Tool && message.Content.Contains("failed"));
    }

    [Fact]
    public async Task GatherAsync_DeduplicatesIdenticalCalls()
    {
        var client = new ScriptedLlmClient("[]",
        [
            new LlmToolResponse(string.Empty, "tool_calls", [new LlmToolCall("c1", "characters", "{}")]),
            new LlmToolResponse(string.Empty, "tool_calls", [new LlmToolCall("c2", "characters", "{}")]),
            new LlmToolResponse(string.Empty, "stop", []),
        ]);
        var agent = new ToolAgent(client);
        var request = new LlmRequest { Model = "m", Messages = [LlmMessage.User("hi")] };
        var invoked = 0;

        var outcome = await agent.GatherAsync(
            _connection, request, [new LlmTool("characters", "d", new JsonObject())], (_, _) => { invoked++; return "Aria"; }, maxCalls: 5);

        Assert.Equal(1, invoked);
        Assert.Equal(2, outcome.ToolCalls);
        Assert.Contains(outcome.Messages, message => message.ToolCallId == "c2" && message.Content == "Aria");
    }

    [Fact]
    public async Task GatherAsync_BoundsAccumulatedResults()
    {
        var client = new ScriptedLlmClient("[]",
        [
            new LlmToolResponse(string.Empty, "tool_calls", [new LlmToolCall("c1", "characters", "{}")]),
            new LlmToolResponse(string.Empty, "tool_calls", [new LlmToolCall("c2", "characters", "{\"x\":1}")]),
            new LlmToolResponse(string.Empty, "stop", []),
        ]);
        var agent = new ToolAgent(client);
        var request = new LlmRequest { Model = "m", Messages = [LlmMessage.User("hi")] };

        var outcome = await agent.GatherAsync(
            _connection, request, [new LlmTool("characters", "d", new JsonObject())], (_, _) => new string('a', 100), maxCalls: 5, maxResultChars: 10);

        Assert.Contains(outcome.Messages, message => message.ToolCallId == "c1" && message.Content.Contains("truncated"));
        Assert.Contains(outcome.Messages, message => message.ToolCallId == "c2" && message.Content.Contains("budget"));
    }
}
