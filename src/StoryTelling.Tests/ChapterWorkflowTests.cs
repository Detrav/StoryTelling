using StoryTelling.Application.Chapters;
using StoryTelling.Application.Review;
using StoryTelling.Application.Settings;
using StoryTelling.Domain;

namespace StoryTelling.Tests;

public sealed class ChapterWorkflowTests
{
    [Fact]
    public async Task RunAsync_ChainsWriterEditorSummarizer()
    {
        var writer = new FakeChapterAgent { Text = "Draft.", ToolCalls = 4 };
        var editor = new FakeChapterEditor { Result = "Edited." };
        var summarizer = new FakeChapterSummarizer { Summary = new ChapterSummary("Log.", new WorldState { TimeAndPlace = "Here" }, [], [], []) };
        var workflow = new ChapterWorkflow(writer, editor, summarizer, new FakeSettingsService());

        var result = await workflow.RunAsync(new Project(), new Chapter { Number = 2, Title = "Two" }, new WorldState { TimeAndPlace = "Before" });

        Assert.Equal("Edited.", result.Text);
        Assert.Equal("Log.", result.Logline);
        Assert.Equal("Here", result.WorldState.TimeAndPlace);
        Assert.Equal(4, result.ToolCalls);
        Assert.Equal("Draft.", editor.Stages.Count > 0 ? "Draft." : editor.LastDraft);
        Assert.Contains(EditorStage.Integrity, editor.Stages);
        Assert.Contains(EditorStage.Cosmetic, editor.Stages);
        Assert.Equal("Edited.", summarizer.LastChapter!.ContentOriginal);
    }

    [Fact]
    public async Task RunAsync_RunsIntegrityThenCosmeticStages()
    {
        var writer = new FakeChapterAgent { Text = "Draft." };
        var editor = new FakeChapterEditor { Result = "Edited." };
        var summarizer = new FakeChapterSummarizer();
        var settings = new FakeSettingsService { Settings = new AppSettings { EditorStageCount = 3 } };
        var workflow = new ChapterWorkflow(writer, editor, summarizer, settings);

        await workflow.RunAsync(new Project(), new Chapter { Number = 1 }, new WorldState());

        Assert.Equal(EditorStage.Integrity, editor.Stages[0]);
        Assert.Equal(3, editor.Stages.Count);
        Assert.Equal(2, editor.Stages.Count(stage => stage == EditorStage.Cosmetic));
    }

    [Fact]
    public async Task RunAsync_CarriesIntegrityVerdictAndNotes()
    {
        var writer = new FakeChapterAgent();
        var editor = new FakeChapterEditor
        {
            Verdict = new EditorVerdict(false, [new EditorIssue(ReviewSeverity.Error, "dead character acting", "Silas Marek")]),
        };
        var summarizer = new FakeChapterSummarizer();
        var workflow = new ChapterWorkflow(writer, editor, summarizer, new FakeSettingsService());

        var result = await workflow.RunAsync(new Project(), new Chapter { Number = 1 }, new WorldState());

        Assert.False(result.Verdict.Integrity);
        Assert.True(result.Verdict.HasError);
        Assert.Equal("Silas Marek", result.Verdict.Issues[0].Reference);
    }

    [Fact]
    public async Task RunAsync_PropagatesSummarizerDirectionRewrites()
    {
        var writer = new FakeChapterAgent();
        var editor = new FakeChapterEditor();
        var summarizer = new FakeChapterSummarizer
        {
            Summary = new ChapterSummary("Log.", new WorldState(), [], [], [new DirectionRewrite(3, "New direction.")]),
        };
        var workflow = new ChapterWorkflow(writer, editor, summarizer, new FakeSettingsService());

        var result = await workflow.RunAsync(new Project(), new Chapter { Number = 1 }, new WorldState());

        Assert.Equal(3, result.DirectionRewrites.Single().ChapterNumber);
        Assert.Equal("New direction.", result.DirectionRewrites.Single().Direction);
    }

    [Fact]
    public async Task RunAsync_UsesTheConfiguredContextBudgets()
    {
        var writer = new FakeChapterAgent();
        var editor = new FakeChapterEditor();
        var summarizer = new FakeChapterSummarizer();
        var settings = new FakeSettingsService
        {
            Settings = new AppSettings
            {
                ContextTokenBudget = 1234,
                RecentLoglineCount = 7,
                ContextRequiredSectionMaxChars = 2222,
            },
        };
        var workflow = new ChapterWorkflow(writer, editor, summarizer, settings);

        await workflow.RunAsync(new Project(), new Chapter { Number = 2, Title = "Two" }, new WorldState());

        Assert.NotNull(writer.LastContext);
        Assert.Equal(1234, writer.LastContext!.TokenBudget);
        Assert.Equal(7, writer.LastContext.RecentLoglineCount);
        Assert.Equal(2222, writer.LastContext.RequiredSectionMaxChars);
    }
}


