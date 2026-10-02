using StoryTelling.Application.Chapters;
using StoryTelling.Infrastructure;

namespace StoryTelling.Tests;

public sealed class ChapterContextExampleTests
{
    [Fact]
    public async Task AssembleWriter_OnTheLargeExample_CarriesPositionAndStorySoFar()
    {
        var project = await new JsonProjectRepository().LoadAsync(ExamplePath("dirty-mirrortag.story.json"));
        var chapter = project.Chapters[^1];
        var index = project.Chapters.IndexOf(chapter);
        var stateBefore = index > 0 ? project.Chapters[index - 1].WorldState ?? project.InitialWorldState : project.InitialWorldState;
        var assembler = new ChapterContextAssembler();

        var context = assembler.AssembleWriter(new WriterContext(project, chapter, stateBefore));

        var user = context.Messages[1].Content;
        Assert.Contains($"chapter {chapter.Number} of {project.Chapters.Count}", user);
        Assert.Contains("Story so far:", user);
        Assert.Contains("The Reflection That Did", user);
        Assert.Contains("Car That Ran Through Its Own Shadow", user);
        Assert.Contains("Mirror Registry", user);
        Assert.Contains("Crack in Every Glass Surface", user);
        Assert.True(context.EstimatedTokens > 0);
    }

    [Fact]
    public async Task AssembleWriter_OnTheLargeExample_StaysWithinAReasonableSeed()
    {
        var project = await new JsonProjectRepository().LoadAsync(ExamplePath("dirty-mirrortag.story.json"));
        var chapter = project.Chapters[^1];
        var assembler = new ChapterContextAssembler();

        var context = assembler.AssembleWriter(new WriterContext(project, chapter, project.InitialWorldState));

        Assert.InRange(context.EstimatedTokens, 1, ChapterContextAssembler.DefaultTokenBudget * 2);
    }

    private static string ExamplePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "examples", name);
}
