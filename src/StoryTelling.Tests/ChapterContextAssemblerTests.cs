using StoryTelling.Application.Chapters;
using StoryTelling.Application.Llm;
using StoryTelling.Application.Prompts;
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

        var user = UserText(context);
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

        Assert.Contains("[truncated]", UserText(context));
    }

    [Fact]
    public void AssembleWriter_PositionsTheChapterAndCarriesTheStorySoFar()
    {
        var project = ProjectWithLoglines(4);
        var assembler = new ChapterContextAssembler();

        var context = assembler.AssembleWriter(new WriterContext(project, project.Chapters[3], project.InitialWorldState, RecentLoglineCount: 3));

        var user = UserText(context);
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

        var user = UserText(context);
        Assert.Contains("chapter 6 of 6", user);
        Assert.Contains("Log line 1.", user);
        Assert.Contains("Log line 4.", user);
        Assert.Contains("Log line 5.", user);
        Assert.DoesNotContain("Log line 2.", user);
        Assert.DoesNotContain("Log line 3.", user);
        Assert.Contains("omitted", user);
    }

    [Fact]
    public void AssembleWriter_IncludesTheRunningStorySoFarAlongsideLoglines()
    {
        var project = ProjectWithLoglines(3);
        project.Chapters[0].StorySoFar = "The running retelling.";
        var assembler = new ChapterContextAssembler();

        var context = assembler.AssembleWriter(new WriterContext(project, project.Chapters[2], project.InitialWorldState));

        var user = UserText(context);
        Assert.Contains("The running retelling.", user);
        Assert.Contains("Recent chapters:", user);
        Assert.Contains("Log line 1.", user);
        Assert.Contains("Log line 2.", user);
    }

    [Fact]
    public void AssembleWriter_FirstChapter_FramesItAsTheSetup()
    {
        var project = ProjectWithLoglines(4);
        var assembler = new ChapterContextAssembler();

        var context = assembler.AssembleWriter(new WriterContext(project, project.Chapters[0], project.InitialWorldState));

        var user = UserText(context);
        Assert.Contains("opening chapter", user);
        Assert.Contains("setup of the story", user);
    }

    [Fact]
    public void AssembleWriter_FinalChapter_FramesItAsTheResolution()
    {
        var project = ProjectWithLoglines(4);
        var assembler = new ChapterContextAssembler();

        var context = assembler.AssembleWriter(new WriterContext(project, project.Chapters[3], project.InitialWorldState));

        var user = UserText(context);
        Assert.Contains("final chapter", user);
        Assert.Contains("resolution", user);
        Assert.Contains("cliffhanger", user);
    }

    [Fact]
    public void AssembleWriter_BothWithRetelling_UsesOnlyTheLatestLoglines()
    {
        var project = ProjectWithLoglines(4);
        project.Chapters[0].StorySoFar = "The running retelling.";
        var assembler = new ChapterContextAssembler();

        var context = assembler.AssembleWriter(new WriterContext(project, project.Chapters[3], project.InitialWorldState, RecentLoglineCount: 1));

        var user = UserText(context);
        Assert.Contains("The running retelling.", user);
        Assert.Contains("Recent chapters:", user);
        Assert.Contains("Log line 3.", user);
        Assert.DoesNotContain("Log line 1.", user);
    }

    [Fact]
    public void AssembleWriter_LoglinesMode_OmitsTheRunningRetelling()
    {
        var project = ProjectWithLoglines(3);
        project.Chapters[0].StorySoFar = "The running retelling.";
        var assembler = new ChapterContextAssembler();

        var context = assembler.AssembleWriter(new WriterContext(project, project.Chapters[2], project.InitialWorldState, StorySoFarMode: "Loglines"));

        var user = UserText(context);
        Assert.Contains("Log line 1.", user);
        Assert.DoesNotContain("The running retelling.", user);
    }

    [Fact]
    public void AssembleWriter_RetellingMode_OmitsTheLoglines()
    {
        var project = ProjectWithLoglines(3);
        project.Chapters[0].StorySoFar = "The running retelling.";
        var assembler = new ChapterContextAssembler();

        var context = assembler.AssembleWriter(new WriterContext(project, project.Chapters[2], project.InitialWorldState, StorySoFarMode: "Retelling"));

        var user = UserText(context);
        Assert.Contains("The running retelling.", user);
        Assert.DoesNotContain("Log line 1.", user);
        Assert.DoesNotContain("Recent chapters:", user);
    }

    [Fact]
    public void AssembleWriter_ExplicitRole_OverridesTheDerivedPosition()
    {
        var project = ProjectWithLoglines(4);
        project.Chapters[1].Role = ChapterRole.Finale;
        var assembler = new ChapterContextAssembler();

        var context = assembler.AssembleWriter(new WriterContext(project, project.Chapters[1], project.InitialWorldState));

        Assert.Contains("final chapter", UserText(context));
        Assert.Contains("chapter 2 of 4", UserText(context));
    }

    [Fact]
    public void AssembleWriter_IgnoresLoglinesOfLaterChapters()
    {
        var project = ProjectWithLoglines(4);
        var assembler = new ChapterContextAssembler();

        var context = assembler.AssembleWriter(new WriterContext(project, project.Chapters[1], project.InitialWorldState));

        var user = UserText(context);
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

    [Fact]
    public void BuildEditorSeed_IncludesTheRunningStorySoFarAndPosition()
    {
        var project = ProjectWithLoglines(3);
        project.Chapters[0].StorySoFar = "The running retelling.";

        var messages = PromptTemplates.BuildEditorSeed(project.Chapters[2], project.InitialWorldState, project);

        Assert.Contains("The running retelling.", messages[1].Content);
        Assert.Contains("chapter 3 of 3", messages[1].Content);
        Assert.Contains("Recent chapters:", messages[1].Content);
    }

    private static string UserText(ChapterContext context) =>
        string.Join("\n", context.Messages.Where(message => message.Role == LlmRole.User).Select(message => message.Content));

    private static Project Project() => new()
    {
        Name = "The Ember Crown",
        World = new World { Tone = "grim", Title = "Ashen Reach", Body = "A dying empire." },
        Knowledge = [new KnowledgeEntry { Kind = KnowledgeKind.Character, Title = "Aria", Tags = ["scout"] }],
        InitialWorldState = new WorldState { TimeAndPlace = "Dusk above the keep", Description = "Aria crouches in the ruins." },
        Chapters = [new Chapter { Number = 1, Title = "Embers", Direction = "Open quietly.", ContentOriginal = "Secret previous prose" }],
    };
}
