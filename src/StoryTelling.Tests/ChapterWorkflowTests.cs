using StoryTelling.Application.Chapters;
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
        var workflow = new ChapterWorkflow(writer, editor, summarizer);

        var result = await workflow.RunAsync(new Project(), new Chapter { Number = 2, Title = "Two" }, new WorldState { TimeAndPlace = "Before" });

        Assert.Equal("Edited.", result.Text);
        Assert.Equal("Log.", result.Logline);
        Assert.Equal("Here", result.WorldState.TimeAndPlace);
        Assert.Equal(4, result.ToolCalls);
        Assert.Equal("Draft.", editor.LastDraft);
        Assert.Equal("Edited.", summarizer.LastChapter!.ContentOriginal);
    }
}
