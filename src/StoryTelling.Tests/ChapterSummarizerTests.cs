using StoryTelling.Application.Chapters;
using StoryTelling.Application.Llm;
using StoryTelling.Application.Prompts;
using StoryTelling.Domain;

namespace StoryTelling.Tests;

public sealed class ChapterSummarizerTests
{
    [Fact]
    public async Task SummarizeAsync_ParsesLoglineAndState()
    {
        const string json = """{"logline":"Rurka finds Sethan alive.","timeAndPlace":"Inside the collapsed vault, night","description":"They are trapped but together.","knowledgeChanges":[]}""";
        var summarizer = new ChapterSummarizer(new FakeLlmClient(json), new FakeSettingsService());
        var chapter = new Chapter { Number = 3, ContentOriginal = "text" };

        var summary = await summarizer.SummarizeAsync(chapter, new WorldState { TimeAndPlace = "Dusk" }, []);

        Assert.Equal("Rurka finds Sethan alive.", summary.Logline);
        Assert.Equal("Inside the collapsed vault, night", summary.WorldState.TimeAndPlace);
        Assert.Equal("They are trapped but together.", summary.WorldState.Description);
        Assert.Empty(summary.KnowledgeChanges);
    }

    [Fact]
    public async Task SummarizeAsync_ParsesKnowledgeChanges()
    {
        const string json = """{"logline":"Sethan dies.","timeAndPlace":"The vault","description":"Grief.","knowledgeChanges":[{"operation":"Update","title":"Sethan","kind":"Character","tags":["deceased"],"content":"Sethan, now dead.","reason":"He dies in the vault."},{"operation":"Delete","title":"Old Map","kind":"Item","tags":[],"content":"","reason":"Destroyed."},{"operation":"Bogus","title":"Ignored","kind":"Note","tags":[],"content":"","reason":""}]}""";
        var summarizer = new ChapterSummarizer(new FakeLlmClient(json), new FakeSettingsService());

        var summary = await summarizer.SummarizeAsync(new Chapter { Number = 3, ContentOriginal = "text" }, new WorldState(), []);

        Assert.Equal(2, summary.KnowledgeChanges.Count);
        Assert.Equal(KnowledgeChangeOperation.Update, summary.KnowledgeChanges[0].Operation);
        Assert.Equal("Sethan", summary.KnowledgeChanges[0].Title);
        Assert.Equal(KnowledgeKind.Character, summary.KnowledgeChanges[0].Kind);
        Assert.Contains("deceased", summary.KnowledgeChanges[0].Tags);
        Assert.Equal(KnowledgeChangeOperation.Delete, summary.KnowledgeChanges[1].Operation);
    }

    [Fact]
    public async Task SummarizeAsync_StripsKindPrefixFromTitle()
    {
        const string json = """{"logline":"l","timeAndPlace":"t","description":"d","knowledgeChanges":[{"operation":"Update","title":"[Character] Mira Vale","kind":"Character","tags":[],"content":"x","reason":"r"}]}""";
        var summarizer = new ChapterSummarizer(new FakeLlmClient(json), new FakeSettingsService());

        var summary = await summarizer.SummarizeAsync(new Chapter { Number = 1, ContentOriginal = "t" }, new WorldState(), []);

        Assert.Equal("Mira Vale", Assert.Single(summary.KnowledgeChanges).Title);
    }

    [Fact]
    public async Task SummarizeAsync_RetriesThenSucceeds()
    {
        const string junk = """{"logline":"},{","timeAndPlace":"x","description":"y"}""";
        const string good = """{"logline":"ok","timeAndPlace":"here","description":"now","knowledgeChanges":[]}""";
        var llm = new FakeLlmClient(good);
        llm.JsonQueue.Enqueue(junk);
        var summarizer = new ChapterSummarizer(llm, new FakeSettingsService());

        var summary = await summarizer.SummarizeAsync(new Chapter { Number = 1, ContentOriginal = "t" }, new WorldState(), []);

        Assert.Equal("ok", summary.Logline);
        Assert.Equal(2, llm.JsonCallCount);
    }

    [Fact]
    public async Task SummarizeAsync_AllInvalid_Throws()
    {
        var llm = new FakeLlmClient("""{"logline":"},{","timeAndPlace":"x","description":"y"}""");
        var summarizer = new ChapterSummarizer(llm, new FakeSettingsService());

        var exception = await Assert.ThrowsAsync<LlmException>(() =>
            summarizer.SummarizeAsync(new Chapter { Number = 1, ContentOriginal = "t" }, new WorldState(), []));

        Assert.Equal(LlmErrorKind.InvalidResponse, exception.Kind);
        Assert.Equal(ChapterSummarizer.MaxStructuredAttempts, llm.JsonCallCount);
    }

    [Fact]
    public void BuildSummarizer_IncludesChapterTextAndPreviousState()
    {
        var messages = PromptTemplates.BuildSummarizer(
            new Chapter { Number = 2, ContentOriginal = "The river ran red." },
            new WorldState { TimeAndPlace = "Dawn over the bridge" },
            [new KnowledgeEntry { Kind = KnowledgeKind.Character, Title = "Sethan", Content = "A scout." }]);

        Assert.Contains("The river ran red.", messages[1].Content);
        Assert.Contains("Dawn over the bridge", messages[1].Content);
        Assert.Contains("Sethan", messages[1].Content);
        Assert.Contains("knowledgeChanges", messages[1].Content);
    }
}
