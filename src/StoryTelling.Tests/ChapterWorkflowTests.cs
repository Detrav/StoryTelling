using StoryTelling.Application.Chapters;
using StoryTelling.Application.Settings;
using StoryTelling.Domain;

namespace StoryTelling.Tests;

public sealed class ChapterWorkflowTests
{
    [Fact]
    public async Task RunAsync_ChainsWriterCheckFixCosmeticSummarizer()
    {
        var writer = new FakeChapterAgent { Text = "Draft.", ToolCalls = 4 };
        var editor = new FakeChapterEditor
        {
            Verdict = new EditorChecklistVerdict([new EditorCheckResult("status", false, "alive character killed")]),
            FixResult = "Fixed.",
            CosmeticResult = "Edited.",
        };
        var summarizer = new FakeChapterSummarizer { Summary = new ChapterSummary("Log.", new WorldState { TimeAndPlace = "Here" }, [], [], []) };
        var workflow = new ChapterWorkflow(writer, editor, summarizer, new FakeSettingsService());

        var result = await workflow.RunAsync(new Project(), new Chapter { Number = 2, Title = "Two" }, new WorldState { TimeAndPlace = "Before" });

        Assert.Equal("Edited.", result.Text);
        Assert.Equal("Log.", result.Logline);
        Assert.Equal(4, result.ToolCalls);
        Assert.Equal("status", Assert.Single(editor.Fixed).Id);
        Assert.True(editor.CosmeticCalled);
        Assert.Equal("Edited.", summarizer.LastChapter!.ContentOriginal);
        Assert.Equal("status", Assert.Single(result.Checklist.Failures).Id);
    }

    [Fact]
    public async Task RunAsync_AllOk_SkipsFixers()
    {
        var writer = new FakeChapterAgent();
        var editor = new FakeChapterEditor
        {
            Verdict = new EditorChecklistVerdict([new EditorCheckResult("world-canon", true, "")]),
            CosmeticResult = "Edited.",
        };
        var workflow = new ChapterWorkflow(writer, editor, new FakeChapterSummarizer(), new FakeSettingsService());

        await workflow.RunAsync(new Project(), new Chapter { Number = 1 }, new WorldState());

        Assert.Empty(editor.Fixed);
    }

    [Fact]
    public async Task RunAsync_SequentialFixersSeeTheUpdatedText()
    {
        var writer = new FakeChapterAgent();
        var editor = new FakeChapterEditor
        {
            Verdict = new EditorChecklistVerdict(
            [
                new EditorCheckResult("status", false, "a"),
                new EditorCheckResult("pov-tense", false, "b"),
            ]),
            FixResult = "Fixed.",
            CosmeticResult = "Edited.",
        };
        var workflow = new ChapterWorkflow(writer, editor, new FakeChapterSummarizer(), new FakeSettingsService());

        await workflow.RunAsync(new Project(), new Chapter { Number = 1 }, new WorldState());

        Assert.Equal(2, editor.Fixed.Count);
        Assert.Equal("Generated chapter text.", editor.FixInputs[0]);
        Assert.Equal("Fixed.", editor.FixInputs[1]);
    }

    [Fact]
    public async Task RunAsync_CosmeticDisabled_KeepsFixerText()
    {
        var writer = new FakeChapterAgent { Text = "Draft." };
        var editor = new FakeChapterEditor
        {
            Verdict = new EditorChecklistVerdict([new EditorCheckResult("status", false, "x")]),
            FixResult = "Fixed.",
        };
        var settings = new FakeSettingsService { Settings = new AppSettings { CosmeticEditorEnabled = false } };
        var workflow = new ChapterWorkflow(writer, editor, new FakeChapterSummarizer(), settings);

        var result = await workflow.RunAsync(new Project(), new Chapter { Number = 1 }, new WorldState());

        Assert.Equal("Fixed.", result.Text);
        Assert.False(editor.CosmeticCalled);
    }

    [Fact]
    public async Task RunAsync_UsesTheConfiguredContextBudgets()
    {
        var writer = new FakeChapterAgent();
        var settings = new FakeSettingsService
        {
            Settings = new AppSettings
            {
                ContextTokenBudget = 1234,
                RecentLoglineCount = 7,
                ContextRequiredSectionMaxChars = 2222,
            },
        };
        var workflow = new ChapterWorkflow(writer, new FakeChapterEditor(), new FakeChapterSummarizer(), settings);

        await workflow.RunAsync(new Project(), new Chapter { Number = 2, Title = "Two" }, new WorldState());

        Assert.NotNull(writer.LastContext);
        Assert.Equal(1234, writer.LastContext!.TokenBudget);
        Assert.Equal(7, writer.LastContext.RecentLoglineCount);
        Assert.Equal(2222, writer.LastContext.RequiredSectionMaxChars);
    }
}
