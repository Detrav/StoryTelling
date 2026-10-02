using StoryTelling.Application.Chapters;
using StoryTelling.Application.Llm;
using StoryTelling.Application.Prompts;
using StoryTelling.Domain;

namespace StoryTelling.Tests;

public sealed class ChapterSummarizerTests
{
    [Fact]
    public async Task SummarizeAsync_ParsesLoglineStateAndStorySoFar()
    {
        const string json = """{"logline":"Rurka finds Sethan alive.","timeAndPlace":"Inside the collapsed vault, night","description":"They are trapped but together.","storySoFar":"Rurka and Sethan are trapped together in the vault.","knowledgeChanges":[]}""";
        var summarizer = new ChapterSummarizer(new FakeLlmClient(json), new FakeSettingsService());
        var chapter = new Chapter { Number = 3, ContentOriginal = "text" };

        var summary = await summarizer.SummarizeAsync(chapter, new WorldState { TimeAndPlace = "Dusk" }, []);

        Assert.Equal("Rurka finds Sethan alive.", summary.Logline);
        Assert.Equal("Inside the collapsed vault, night", summary.WorldState.TimeAndPlace);
        Assert.Equal("They are trapped but together.", summary.WorldState.Description);
        Assert.Equal("Rurka and Sethan are trapped together in the vault.", summary.StorySoFar);
        Assert.Empty(summary.KnowledgeChanges);
    }

    [Fact]
    public async Task SummarizeAsync_ParsesKnowledgeChanges()
    {
        const string json = """{"logline":"Sethan dies.","timeAndPlace":"The vault","description":"Grief.","storySoFar":"Sethan dies in the vault.","knowledgeChanges":[{"operation":"Update","title":"Sethan","kind":"Character","tags":["deceased"],"content":"Sethan, now dead.","reason":"He dies in the vault."},{"operation":"Delete","title":"Old Map","kind":"Item","tags":[],"content":"","reason":"Destroyed."},{"operation":"Bogus","title":"Ignored","kind":"Note","tags":[],"content":"","reason":""}]}""";
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
        const string json = """{"logline":"l","timeAndPlace":"t","description":"d","storySoFar":"s","knowledgeChanges":[{"operation":"Update","title":"[Character] Mira Vale","kind":"Character","tags":[],"content":"x","reason":"r"}]}""";
        var summarizer = new ChapterSummarizer(new FakeLlmClient(json), new FakeSettingsService());

        var summary = await summarizer.SummarizeAsync(new Chapter { Number = 1, ContentOriginal = "t" }, new WorldState(), []);

        Assert.Equal("Mira Vale", Assert.Single(summary.KnowledgeChanges).Title);
    }

    [Fact]
    public async Task SummarizeAsync_RetriesTheBriefingThenSucceeds()
    {
        const string junk = """{"logline":"},{","timeAndPlace":"x","description":"y"}""";
        const string good = """{"logline":"ok","timeAndPlace":"here","description":"now","storySoFar":"retold","knowledgeChanges":[]}""";
        var llm = new FakeLlmClient(good);
        llm.JsonQueue.Enqueue(junk);
        var summarizer = new ChapterSummarizer(llm, new FakeSettingsService());

        var summary = await summarizer.SummarizeAsync(new Chapter { Number = 1, ContentOriginal = "t" }, new WorldState(), []);

        Assert.Equal("ok", summary.Logline);
        Assert.Equal("retold", summary.StorySoFar);
        Assert.Equal(3, llm.JsonCallCount);
    }

    [Fact]
    public async Task SummarizeAsync_AllBriefingsInvalid_Throws()
    {
        var llm = new FakeLlmClient("""{"logline":"},{","timeAndPlace":"x","description":"y"}""");
        var summarizer = new ChapterSummarizer(llm, new FakeSettingsService());

        var exception = await Assert.ThrowsAsync<LlmException>(() =>
            summarizer.SummarizeAsync(new Chapter { Number = 1, ContentOriginal = "t" }, new WorldState(), []));

        Assert.Equal(LlmErrorKind.InvalidResponse, exception.Kind);
        Assert.Equal(ChapterSummarizer.MaxStructuredAttempts, llm.JsonCallCount);
    }

    [Fact]
    public async Task SummarizeAsync_UnchangedStorySoFar_IsRetriedAndFallsBack()
    {
        const string briefing = """{"logline":"New events happen.","timeAndPlace":"here","description":"now","storySoFar":"Same text.","knowledgeChanges":[]}""";
        var summarizer = new ChapterSummarizer(new FakeLlmClient(briefing), new FakeSettingsService());

        var summary = await summarizer.SummarizeAsync(
            new Chapter { Number = 2, ContentOriginal = "t" },
            new WorldState(),
            [],
            previousStorySoFar: "Same text.");

        Assert.Equal("Same text.", summary.StorySoFar);
    }

    [Fact]
    public void BuildChapterBriefing_IncludesChapterTextStateAndKnowledge()
    {
        var messages = PromptTemplates.BuildChapterBriefing(
            new Chapter { Number = 2, ContentOriginal = "The river ran red." },
            new WorldState { TimeAndPlace = "Dawn over the bridge" },
            [new KnowledgeEntry { Kind = KnowledgeKind.Character, Title = "Sethan", Content = "A scout." }]);

        Assert.Contains("The river ran red.", messages[1].Content);
        Assert.Contains("Dawn over the bridge", messages[1].Content);
        Assert.Contains("Sethan", messages[1].Content);
        Assert.Contains("knowledgeChanges", messages[1].Content);
    }

    [Fact]
    public void BuildChapterStorySync_IncludesPreviousRetellingAndBriefing()
    {
        var briefing = new ChapterBriefing("Sethan dies.", new WorldState { Description = "Grief in the vault." }, []);

        var messages = PromptTemplates.BuildChapterStorySync("The scout fled north.", briefing, chapterNumber: 3);

        Assert.Contains("The scout fled north.", messages[1].Content);
        Assert.Contains("Sethan dies.", messages[1].Content);
        Assert.Contains("story so far", messages[1].Content);
    }
}
