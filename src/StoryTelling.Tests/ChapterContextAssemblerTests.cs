using StoryTelling.Application.Chapters;
using StoryTelling.Domain;

namespace StoryTelling.Tests;

public sealed class ChapterContextAssemblerTests
{
    [Fact]
    public void AssembleWriter_IncludesBriefFrameStateManifestButNoChapterText()
    {
        var project = Project();
        var assembler = new ChapterContextAssembler();

        var context = assembler.AssembleWriter(new WriterContext(project, project.Chapters[0], project.InitialWorldState, ChapterContextAssembler.DefaultTokenBudget));

        var user = context.Messages[1].Content;
        Assert.Contains("Chapter 1", user);
        Assert.Contains("grim", user);
        Assert.Contains("Dusk above the keep", user);
        Assert.Contains("Aria", user);
        Assert.Contains("Project manifest", user);
        Assert.DoesNotContain("Secret previous prose", user);
        Assert.True(context.EstimatedTokens > 0);
    }

    [Fact]
    public void AssembleWriter_TruncatesOptionalSectionsToBudget()
    {
        var project = Project();
        project.World = new World { Title = "Ashen Reach", Body = new string('x', 5000) };
        var assembler = new ChapterContextAssembler();

        var context = assembler.AssembleWriter(new WriterContext(project, project.Chapters[0], project.InitialWorldState, 200));

        Assert.Contains("[truncated]", context.Messages[1].Content);
    }

    private static Project Project() => new()
    {
        Name = "The Ember Crown",
        World = new World { Tone = "grim", Title = "Ashen Reach", Body = "A dying empire." },
        Knowledge = [new KnowledgeEntry { Kind = KnowledgeKind.Character, Title = "Aria", Tags = ["scout"] }],
        InitialWorldState = new WorldState { TimeAndPlace = "Dusk above the keep", Description = "Aria crouches in the ruins." },
        Chapters = [new Chapter { Number = 1, Title = "Embers", Direction = "Open quietly.", ContentOriginal = "Secret previous prose" }],
    };
}
