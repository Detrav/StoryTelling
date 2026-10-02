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
        const string json = """{"logline":"Rurka finds Sethan alive.","timeAndPlace":"Inside the collapsed vault, night","situation":"They are trapped but together.","knowledgeChanges":[]}""";
        var summarizer = new ChapterSummarizer(new FakeLlmClient(json), new FakeSettingsService());
        var chapter = new Chapter { Number = 3, ContentOriginal = "text" };

        var summary = await summarizer.SummarizeAsync(chapter, new WorldState { TimeAndPlace = "Dusk" }, []);

        Assert.Equal("Rurka finds Sethan alive.", summary.Logline);
        Assert.Equal("Inside the collapsed vault, night", summary.WorldState.TimeAndPlace);
        Assert.Equal("They are trapped but together.", summary.WorldState.Situation);
        Assert.Empty(summary.KnowledgeChanges);
    }

    [Fact]
    public async Task SummarizeAsync_ParsesKnowledgeChanges()
    {
        const string json = """{"logline":"Sethan dies.","timeAndPlace":"The vault","situation":"Grief.","knowledgeChanges":[{"operation":"Update","entryId":"","status":"None","title":"Sethan","kind":"Character","tags":["deceased"],"content":"Sethan, now dead.","reason":"He dies in the vault."},{"operation":"Delete","entryId":"","status":"None","title":"Old Map","kind":"Item","tags":[],"content":"","reason":"Destroyed."},{"operation":"Bogus","title":"Ignored","kind":"Note","tags":[],"content":"","reason":""}]}""";
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
    public async Task SummarizeAsync_ParsesContinuityNotes()
    {
        const string json = """{"logline":"l","timeAndPlace":"t","situation":"s","knowledgeChanges":[],"continuityNotes":[{"severity":"Error","detail":"Silas is alive in the bible but dies in the prose.","reference":"Silas Marek"},{"severity":"Info","detail":"Minor naming drift.","reference":""},{"severity":"Bogus","detail":"ignored","reference":"x"}]}""";
        var summarizer = new ChapterSummarizer(new FakeLlmClient(json), new FakeSettingsService());

        var summary = await summarizer.SummarizeAsync(new Chapter { Number = 3, ContentOriginal = "text" }, new WorldState(), []);

        Assert.Equal(2, summary.ContinuityIssues.Count);
        Assert.Equal(StoryTelling.Application.Review.ReviewSeverity.Error, summary.ContinuityIssues[0].Severity);
        Assert.Equal("Silas Marek", summary.ContinuityIssues[0].Reference);
    }

    [Fact]
    public async Task SummarizeAsync_WithoutContinuityNotes_ReturnsEmpty()
    {
        const string json = """{"logline":"l","timeAndPlace":"t","situation":"s","knowledgeChanges":[]}""";
        var summarizer = new ChapterSummarizer(new FakeLlmClient(json), new FakeSettingsService());

        var summary = await summarizer.SummarizeAsync(new Chapter { Number = 1, ContentOriginal = "text" }, new WorldState(), []);

        Assert.Empty(summary.ContinuityIssues);
    }

    [Fact]
    public async Task SummarizeAsync_ParsesThreadIdAndStatus()
    {
        const string id = "11111111-1111-1111-1111-111111111111";
        string json = $$"""{"logline":"l","timeAndPlace":"t","situation":"s","knowledgeChanges":[{"operation":"Update","entryId":"{{id}}","status":"Resolved","title":"Find the traitor","kind":"Thread","tags":[],"content":"Resolved.","reason":"Caught."}]}""";
        var summarizer = new ChapterSummarizer(new FakeLlmClient(json), new FakeSettingsService());

        var summary = await summarizer.SummarizeAsync(new Chapter { Number = 2, ContentOriginal = "t" }, new WorldState(), []);

        var change = Assert.Single(summary.KnowledgeChanges);
        Assert.Equal(KnowledgeKind.Thread, change.Kind);
        Assert.Equal(KnowledgeStatus.Resolved, change.Status);
        Assert.Equal(Guid.Parse(id), change.EntryId);
    }

    [Fact]
    public async Task SummarizeAsync_StatusIsKeptOnlyForThreads()
    {
        const string json = """{"logline":"l","timeAndPlace":"t","situation":"s","knowledgeChanges":[{"operation":"Update","entryId":"","status":"Resolved","title":"Elias","kind":"Character","tags":[],"content":"x","reason":"r"},{"operation":"Update","entryId":"","status":"Resolved","title":"Killer","kind":"Thread","tags":[],"content":"y","reason":"r"}]}""";
        var summarizer = new ChapterSummarizer(new FakeLlmClient(json), new FakeSettingsService());

        var summary = await summarizer.SummarizeAsync(new Chapter { Number = 1, ContentOriginal = "t" }, new WorldState(), []);

        Assert.Null(summary.KnowledgeChanges[0].Status);
        Assert.Equal(KnowledgeStatus.Resolved, summary.KnowledgeChanges[1].Status);
    }

    [Fact]
    public async Task SummarizeAsync_StripsTrailingTagSuffixFromTitle()
    {
        const string json = """{"logline":"l","timeAndPlace":"t","situation":"s","knowledgeChanges":[{"operation":"Update","entryId":"","status":"Open","title":"The Killer Behind Morozov's Son [mystery, murder, Weavers]","kind":"Thread","tags":["mystery"],"content":"x","reason":"r"}]}""";
        var summarizer = new ChapterSummarizer(new FakeLlmClient(json), new FakeSettingsService());

        var summary = await summarizer.SummarizeAsync(new Chapter { Number = 1, ContentOriginal = "t" }, new WorldState(), []);

        Assert.Equal("The Killer Behind Morozov's Son", Assert.Single(summary.KnowledgeChanges).Title);
    }

    [Fact]
    public async Task SummarizeAsync_StripsKindPrefixFromTitle()
    {
        const string json = """{"logline":"l","timeAndPlace":"t","situation":"s","knowledgeChanges":[{"operation":"Update","title":"[Character] Mira Vale","kind":"Character","tags":[],"content":"x","reason":"r"}]}""";
        var summarizer = new ChapterSummarizer(new FakeLlmClient(json), new FakeSettingsService());

        var summary = await summarizer.SummarizeAsync(new Chapter { Number = 1, ContentOriginal = "t" }, new WorldState(), []);

        Assert.Equal("Mira Vale", Assert.Single(summary.KnowledgeChanges).Title);
    }

    [Fact]
    public async Task SummarizeAsync_RetriesThenSucceeds()
    {
        const string junk = """{"logline":"},{","timeAndPlace":"x","situation":"y"}""";
        const string good = """{"logline":"ok","timeAndPlace":"here","situation":"now","knowledgeChanges":[]}""";
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
        var llm = new FakeLlmClient("""{"logline":"},{","timeAndPlace":"x","situation":"y"}""");
        var summarizer = new ChapterSummarizer(llm, new FakeSettingsService());

        var exception = await Assert.ThrowsAsync<LlmException>(() =>
            summarizer.SummarizeAsync(new Chapter { Number = 1, ContentOriginal = "t" }, new WorldState(), []));

        Assert.Equal(LlmErrorKind.InvalidResponse, exception.Kind);
        Assert.Equal(ChapterSummarizer.MaxStructuredAttempts, llm.JsonCallCount);
    }

    [Fact]
    public async Task SummarizeAsync_MetaInKnowledgeContent_IsSanitized()
    {
        const string json = """{"logline":"l","timeAndPlace":"t","situation":"s","knowledgeChanges":[{"operation":"Create","entryId":"","status":"None","title":"Reveal","kind":"Note","tags":[],"content":"Revealed in Chapter 6 that Dmitri lives.","reason":"r"}]}""";
        var summarizer = new ChapterSummarizer(new FakeLlmClient(json), new FakeSettingsService());

        var summary = await summarizer.SummarizeAsync(new Chapter { Number = 6, ContentOriginal = "t" }, new WorldState(), []);

        Assert.DoesNotContain("Chapter 6", Assert.Single(summary.KnowledgeChanges).Content);
    }

    [Fact]
    public void BuildChapterSummary_IncludesChapterTextStateAndKnowledge()
    {
        var messages = PromptTemplates.BuildChapterSummary(
            new Chapter { Number = 2, ContentOriginal = "The river ran red." },
            new WorldState { TimeAndPlace = "Dawn over the bridge" },
            [new KnowledgeEntry { Kind = KnowledgeKind.Character, Title = "Sethan", Content = "A scout." }]);

        Assert.Contains("The river ran red.", messages[1].Content);
        Assert.Contains("Dawn over the bridge", messages[1].Content);
        Assert.Contains("Sethan", messages[1].Content);
        Assert.Contains("knowledgeChanges", messages[1].Content);
    }
}
