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

    [Fact]
    public void AssembleWriter_PositionsTheChapterAndCarriesTheStorySoFar()
    {
        var project = ProjectWithLoglines(4);
        var assembler = new ChapterContextAssembler();

        var context = assembler.AssembleWriter(new WriterContext(project, project.Chapters[3], project.InitialWorldState, RecentLoglineCount: 3));

        var user = context.Messages[1].Content;
        Assert.Contains("chapter 4 of 4", user);
        Assert.Contains("Story so far:", user);
        Assert.Contains("Log line 1.", user);
        Assert.Contains("Log line 2.", user);
        Assert.Contains("Log line 3.", user);
        Assert.DoesNotContain("Log line 4.", user);
        Assert.DoesNotContain("Secret previous prose", user);
    }

    [Fact]
    public void AssembleWriter_RecentLoglineCount_KeepsTheFirstAndTheLatestWithAnOmittedMarker()
    {
        var project = ProjectWithLoglines(6);
        var assembler = new ChapterContextAssembler();

        var context = assembler.AssembleWriter(new WriterContext(project, project.Chapters[5], project.InitialWorldState, RecentLoglineCount: 2));

        var user = context.Messages[1].Content;
        Assert.Contains("chapter 6 of 6", user);
        Assert.Contains("Log line 1.", user);
        Assert.Contains("Log line 4.", user);
        Assert.Contains("Log line 5.", user);
        Assert.DoesNotContain("Log line 2.", user);
        Assert.DoesNotContain("Log line 3.", user);
        Assert.Contains("omitted", user);
    }

    [Fact]
    public void AssembleWriter_IgnoresLoglinesOfLaterChapters()
    {
        var project = ProjectWithLoglines(4);
        var assembler = new ChapterContextAssembler();

        var context = assembler.AssembleWriter(new WriterContext(project, project.Chapters[1], project.InitialWorldState));

        var user = context.Messages[1].Content;
        Assert.Contains("chapter 2 of 4", user);
        Assert.Contains("Log line 1.", user);
        Assert.DoesNotContain("Log line 3.", user);
        Assert.DoesNotContain("Log line 4.", user);
    }

    private static Project ProjectWithLoglines(int chapters) => new()
    {
        Name = "Series",
        World = new World { Tone = "grim" },
        InitialWorldState = new WorldState { TimeAndPlace = "Start", Description = "Begin." },
        Chapters = [.. Enumerable.Range(1, chapters).Select(number => new Chapter
        {
            Number = number,
            Title = $"Title {number}",
            Direction = $"Direction {number}",
            ContentOriginal = "Secret previous prose",
            Logline = $"Log line {number}.",
        })],
    };

    private static Project Project() => new()
    {
        Name = "The Ember Crown",
        World = new World { Tone = "grim", Title = "Ashen Reach", Body = "A dying empire." },
        Knowledge = [new KnowledgeEntry { Kind = KnowledgeKind.Character, Title = "Aria", Tags = ["scout"] }],
        InitialWorldState = new WorldState { TimeAndPlace = "Dusk above the keep", Description = "Aria crouches in the ruins." },
        Chapters = [new Chapter { Number = 1, Title = "Embers", Direction = "Open quietly.", ContentOriginal = "Secret previous prose" }],
    };
}
