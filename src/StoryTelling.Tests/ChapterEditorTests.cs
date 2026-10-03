using StoryTelling.Application.Abstractions;
using StoryTelling.Application.Chapters;
using StoryTelling.Application.Llm;
using StoryTelling.Application.Settings;
using StoryTelling.Domain;
using StoryTelling.Infrastructure.Diff;

namespace StoryTelling.Tests;

public sealed class ChapterEditorTests
{
    private static ChapterEditor Editor(ILlmClient client, ISettingsService settings) =>
        new(client, settings, new ChapterContextAssembler(), new DiffPlexTextDiff());

    private static WriterContext Context(Chapter chapter) =>
        new(new Project(), chapter, new WorldState());

    [Fact]
    public async Task CheckAsync_ParsesPerAxisVerdict()
    {
        const string json = """{"checks":[{"id":"world-canon","ok":true,"reason":""},{"id":"status","ok":false,"reason":"Silas is dead in the prose"}]}""";
        var client = new FakeLlmClient(json);
        client.JsonQueue.Enqueue(json);
        var editor = Editor(client, new FakeSettingsService());

        var verdict = await editor.CheckAsync(Context(new Chapter { Number = 1 }), "draft", EditorChecks.All);

        Assert.Equal(2, verdict.Results.Count);
        Assert.False(verdict.Results[1].Ok);
        Assert.Equal("status", Assert.Single(verdict.Failures).Id);
    }

    [Fact]
    public async Task CheckAsync_EmptyProse_ReturnsEmpty()
    {
        var editor = Editor(new FakeLlmClient("{}"), new FakeSettingsService());

        var verdict = await editor.CheckAsync(Context(new Chapter { Number = 1 }), "   ", EditorChecks.All);

        Assert.Empty(verdict.Results);
    }

    [Fact]
    public async Task FixAsync_ReturnsStreamedRevision()
    {
        var client = new FakeLlmClient("unused");
        client.StreamQueue.Enqueue("Fixed chapter text.");
        var editor = Editor(client, new FakeSettingsService());

        var text = await editor.FixAsync(Context(new Chapter { Number = 1 }), "draft", EditorChecks.Status, "Silas is dead");

        Assert.Equal("Fixed chapter text.", text);
    }

    [Fact]
    public async Task FixAsync_EmptyResponseKeepsDraft()
    {
        var client = new FakeLlmClient("   ");
        client.StreamQueue.Enqueue("   ");
        var editor = Editor(client, new FakeSettingsService());

        var text = await editor.FixAsync(Context(new Chapter { Number = 1 }), "draft", EditorChecks.Status, "x");

        Assert.Equal("draft", text);
    }

    [Fact]
    public async Task CosmeticAsync_ReturnsRevisionAndNotes()
    {
        const string notes = """{"changes":[{"kind":"Style","note":"Tightened prose."}]}""";
        var client = new FakeLlmClient(notes);
        client.JsonQueue.Enqueue(notes);
        client.StreamQueue.Enqueue("Polished chapter text.");
        var editor = Editor(client, new FakeSettingsService());

        var edit = await editor.CosmeticAsync(Context(new Chapter { Number = 1 }), "draft");

        Assert.Equal("Polished chapter text.", edit.Text);
        Assert.Equal("Tightened prose.", Assert.Single(edit.Notes).Text);
    }

    [Fact]
    public async Task CosmeticAsync_StripsLeadingTitleLine()
    {
        var client = new FakeLlmClient("[]");
        client.StreamQueue.Enqueue("Embers\nThe real prose starts here.");
        var editor = Editor(client, new FakeSettingsService());

        var edit = await editor.CosmeticAsync(Context(new Chapter { Number = 1, Title = "Embers" }), "draft");

        Assert.Equal("The real prose starts here.", edit.Text);
    }
}
