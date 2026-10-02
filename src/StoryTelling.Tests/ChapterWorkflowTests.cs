using StoryTelling.Application.Chapters;
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
        var summarizer = new FakeChapterSummarizer { Summary = new ChapterSummary("Log.", new WorldState { TimeAndPlace = "Here" }, []) };
        var workflow = new ChapterWorkflow(writer, editor, summarizer, new FakeSettingsService());

        var result = await workflow.RunAsync(new Project(), new Chapter { Number = 2, Title = "Two" }, new WorldState { TimeAndPlace = "Before" });

        Assert.Equal("Edited.", result.Text);
        Assert.Equal("Log.", result.Logline);
        Assert.Equal("Here", result.WorldState.TimeAndPlace);
        Assert.Equal(4, result.ToolCalls);
        Assert.Equal("Draft.", editor.LastDraft);
        Assert.Equal("Edited.", summarizer.LastChapter!.ContentOriginal);
    }

    [Fact]
    public async Task RunAsync_PassesTheRunningStorySoFarToTheSummarizer()
    {
        var writer = new FakeChapterAgent();
        var editor = new FakeChapterEditor();
        var summarizer = new FakeChapterSummarizer();
        var workflow = new ChapterWorkflow(writer, editor, summarizer, new FakeSettingsService());
        var project = new Project
        {
            Chapters =
            [
                new Chapter { Number = 1, StorySoFar = "The running retelling." },
                new Chapter { Number = 2, Title = "Two" },
            ],
        };

        await workflow.RunAsync(project, project.Chapters[1], new WorldState());

        Assert.Equal("The running retelling.", summarizer.LastPreviousStorySoFar);
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
