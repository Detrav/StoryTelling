using StoryTelling.Application.Abstractions;
using StoryTelling.Application.Chapters;
using StoryTelling.Application.Llm;
using StoryTelling.Application.Settings;
using StoryTelling.Domain;
using StoryTelling.Infrastructure.Diff;

namespace StoryTelling.Tests;

public sealed class ChapterEditorTests
{
    private static ChapterEditor Editor(ILlmClient client, ISettingsService settings) => new(client, settings, new DiffPlexTextDiff());
    [Fact]
    public async Task EditAsync_ReturnsStreamedRevision()
    {
        var editor = Editor(new FakeLlmClient("Revised text."), new FakeSettingsService());

        var text = (await editor.EditAsync(new Project(), new Chapter { Number = 1 }, "the draft", new WorldState(), EditorStage.Integrity)).Text;

        Assert.Equal("Revised text.", text);
    }

    [Fact]
    public async Task EditAsync_EmptyResponseKeepsDraft()
    {
        var editor = Editor(new FakeLlmClient("   "), new FakeSettingsService());

        var text = (await editor.EditAsync(new Project(), new Chapter { Number = 1 }, "the draft", new WorldState(), EditorStage.Integrity)).Text;

        Assert.Equal("the draft", text);
    }

    [Fact]
    public async Task EditAsync_RetriesWhenOutputLooksTruncated()
    {
        var client = new FakeLlmClient("ignored");
        client.StreamQueue.Enqueue("The lighthouse keeper");
        client.StreamQueue.Enqueue("The lighthouse keeper went home.");
        var editor = Editor(client, new FakeSettingsService());

        var text = (await editor.EditAsync(new Project(), new Chapter { Number = 1 }, "draft", new WorldState(), EditorStage.Integrity)).Text;

        Assert.Equal("The lighthouse keeper went home.", text);
        Assert.Equal(2, client.StreamCallCount);
    }

    [Fact]
    public async Task EditAsync_GathersContextWithTools()
    {
        var client = new ScriptedLlmClient(
            jsonResponse: """{"issues":[]}""",
            toolResponses:
            [
                new LlmToolResponse(string.Empty, "tool_calls", [new LlmToolCall("c1", "characters", "{}")]),
                new LlmToolResponse(string.Empty, "stop", []),
            ],
            streamText: "Revised.");
        var editor = Editor(client, new FakeSettingsService { Settings = new AppSettings { Model = "m", MaxToolCalls = 5 } });

        var edit = await editor.EditAsync(new Project(), new Chapter { Number = 1 }, "draft", new WorldState(), EditorStage.Integrity);

        Assert.Equal("Revised.", edit.Text);
        Assert.NotEmpty(client.ToolRequests);
        Assert.Contains(client.ToolRequests[0].Messages, message => message.Content.Contains("inviolable"));
    }

    [Fact]
    public async Task EditAsync_StripsLeadingTitleLine()
    {
        var client = new FakeLlmClient("[]");
        client.StreamQueue.Enqueue("Embers\nThe real prose starts here.");
        var editor = Editor(client, new FakeSettingsService());

        var edit = await editor.EditAsync(new Project(), new Chapter { Number = 1, Title = "Embers" }, "draft", new WorldState(), EditorStage.Integrity);

        Assert.Equal("The real prose starts here.", edit.Text);
    }

    [Fact]
    public async Task EditAsync_ParsesEditorNotes()
    {
        const string notes = """{"changes":[{"kind":"Continuity","note":"Fixed the timeline."},{"kind":"Style","note":"Tightened prose."}]}""";
        var client = new FakeLlmClient(notes);
        client.JsonQueue.Enqueue(notes);
        client.JsonQueue.Enqueue("""{"issues":[]}""");
        client.StreamQueue.Enqueue("Revised chapter text.");
        var editor = Editor(client, new FakeSettingsService());

        var edit = await editor.EditAsync(new Project(), new Chapter { Number = 1 }, "draft", new WorldState(), EditorStage.Integrity);

        Assert.Equal("Revised chapter text.", edit.Text);
        Assert.Equal(2, edit.Notes.Count);
        Assert.Equal(EditorNoteKind.Continuity, edit.Notes[0].Kind);
        Assert.Equal("Tightened prose.", edit.Notes[1].Text);
        Assert.Empty(edit.Verdict.Issues);
    }
}


