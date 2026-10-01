using StoryTelling.Application.Chapters;
using StoryTelling.Application.Llm;
using StoryTelling.Application.Settings;
using StoryTelling.Domain;

namespace StoryTelling.Tests;

public sealed class ChapterAgentTests
{
    [Fact]
    public async Task WriteAsync_GathersWithToolsThenStreamsText()
    {
        var client = new ScriptedLlmClient(
            "{}",
            [
                new LlmToolResponse(string.Empty, "tool_calls", [new LlmToolCall("c1", "characters", "{}")]),
                new LlmToolResponse(string.Empty, "stop", []),
            ],
            "The embers rose over the ridge.");
        var agent = new ChapterAgent(client, new FakeSettingsService { Settings = new AppSettings { Model = "m", MaxToolCalls = 5 } }, new ChapterContextAssembler());
        var project = Project();

        var draft = await agent.WriteAsync(new WriterContext(project, project.Chapters[0], project.WorldState, ChapterContextAssembler.DefaultTokenBudget));

        Assert.Equal("The embers rose over the ridge.", draft.Text);
        Assert.Equal(1, draft.ToolCalls);
        Assert.NotEmpty(client.ToolRequests);
    }

    [Fact]
    public async Task WriteAsync_NoToolsConfigured_StreamsTextDirectly()
    {
        var client = new ScriptedLlmClient("{}", [], "Plain chapter.");
        var agent = new ChapterAgent(client, new FakeSettingsService { Settings = new AppSettings { Model = "m", MaxToolCalls = 0 } }, new ChapterContextAssembler());
        var project = Project();

        var draft = await agent.WriteAsync(new WriterContext(project, project.Chapters[0], project.WorldState, ChapterContextAssembler.DefaultTokenBudget));

        Assert.Equal("Plain chapter.", draft.Text);
        Assert.Equal(0, draft.ToolCalls);
        Assert.Empty(client.ToolRequests);
    }

    private static Project Project() => new()
    {
        Name = "The Ember Crown",
        Frame = new StoryFrame { Tone = "grim" },
        Characters = [new Character { Name = "Aria", Role = "scout" }],
        WorldState = new WorldState { TimeAndPlace = "Dusk above the keep" },
        Chapters = [new Chapter { Number = 1, Title = "Embers" }],
    };
}
