using StoryTelling.Application.Knowledge;
using StoryTelling.Domain;

namespace StoryTelling.Tests;

public sealed class KnowledgeImporterTests
{
    [Fact]
    public async Task ExtractAsync_ParsesEntriesAndKinds()
    {
        const string json = """
        {"entries":[{"kind":"Place","title":"Ashen Reach","tags":["region"],"content":"A frozen frontier."},{"kind":"Item","title":"Ember Crown","tags":[],"content":"A relic of dead magic."}]}
        """;
        var llm = new FakeLlmClient(json);
        var importer = new KnowledgeImporter(llm, new FakeSettingsService());

        var entries = await importer.ExtractAsync(new KnowledgeImportRequest("Some source text.", string.Empty));

        Assert.Equal(2, entries.Count);
        Assert.Equal(KnowledgeKind.Place, entries[0].Kind);
        Assert.Equal("Ashen Reach", entries[0].Title);
        Assert.Contains("region", entries[0].Tags);
        Assert.Equal(KnowledgeKind.Item, entries[1].Kind);
        Assert.Equal("KnowledgeEntries", llm.LastSchemaName);
    }

    [Fact]
    public async Task ExtractAsync_UnknownKind_FallsBackToNote()
    {
        const string json = """{"entries":[{"kind":"Creature","title":"Wyvern","tags":[],"content":"Nests in cliffs."}]}""";
        var importer = new KnowledgeImporter(new FakeLlmClient(json), new FakeSettingsService());

        var entries = await importer.ExtractAsync(new KnowledgeImportRequest("text", string.Empty));

        Assert.Equal(KnowledgeKind.Note, Assert.Single(entries).Kind);
    }

    [Fact]
    public async Task ExtractAsync_MalformedJson_ReturnsEmpty()
    {
        var importer = new KnowledgeImporter(new FakeLlmClient("{ not json"), new FakeSettingsService());

        var entries = await importer.ExtractAsync(new KnowledgeImportRequest("text", string.Empty));

        Assert.Empty(entries);
    }

    [Fact]
    public async Task ExtractAsync_EmptyContent_ReturnsEmpty()
    {
        var importer = new KnowledgeImporter(new FakeLlmClient("{}"), new FakeSettingsService());

        var entries = await importer.ExtractAsync(new KnowledgeImportRequest("   ", string.Empty));

        Assert.Empty(entries);
    }

    [Fact]
    public async Task ExtractAsync_DeduplicatesByTitle()
    {
        const string json = """{"entries":[{"kind":"Note","title":"Aria","tags":[],"content":"first"}]}""";
        var importer = new KnowledgeImporter(new FakeLlmClient(json), new FakeSettingsService());
        var content = $"{new string('a', 1500)}\n\n{new string('b', 1500)}";

        var entries = await importer.ExtractAsync(new KnowledgeImportRequest(content, string.Empty));

        Assert.Single(entries);
    }
}
