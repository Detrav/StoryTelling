using System.Linq;
using StoryTelling.Application.Settings;
using StoryTelling.Domain;
using StoryTelling.Infrastructure.Diff;
using StoryTelling.ViewModels;

namespace StoryTelling.Tests;

public sealed class WorkspaceViewModelTests
{
    private static readonly DateTimeOffset _timestamp = DateTimeOffset.UnixEpoch;

    [Fact]
    public void ToProject_PreservesChapterFields()
    {
        var workspace = new WorkspaceViewModel(SampleProject(), new FakeClock(_timestamp));

        var chapter = Assert.Single(workspace.ToProject().Chapters);

        Assert.Equal(_timestamp, chapter.CreatedUtc);
        Assert.Equal(1, chapter.Number);
        Assert.Equal("Embers", chapter.Title);
        Assert.Equal("Introduction text.", chapter.ContentOriginal);
        Assert.Equal("Дым поднимался.", chapter.Translations["ru"]);
        Assert.Equal(ChapterStatus.Generated, chapter.Status);
        Assert.Equal("Recap.", chapter.Summary);
        Assert.Equal("Logline.", chapter.Logline);
    }

    [Fact]
    public void ApplySetup_DoesNotDestroyDomainData()
    {
        var project = SampleProject();
        var workspace = new WorkspaceViewModel(project, new FakeClock(_timestamp));
        var setup = workspace.CreateSetup(Catalog(), new DiffPlexTextDiff(), new FakeGenerationAssistant());

        workspace.ApplySetup(setup);

        Assert.Equal("Wyverns nest in cliffs.", project.ExtraFiles.Single().Content);
        Assert.Contains("lore", project.ExtraFiles.Single().Tags);
        Assert.Contains("brave", project.Characters.Single().Traits);
        Assert.Equal("unhurt", project.WorldState.Characters.Single().Status);
        Assert.Equal("cliffs", project.WorldState.Characters.Single().Location);
    }

    [Fact]
    public void AddChapter_RenumbersAndMarksDirty()
    {
        var workspace = new WorkspaceViewModel(SampleProject(), new FakeClock(_timestamp));
        workspace.IsDirty = false;

        workspace.AddChapterCommand.Execute(null);

        Assert.Equal(2, workspace.Chapters.Count);
        Assert.Equal(new[] { 1, 2 }, workspace.Chapters.Select(chapter => chapter.Number));
        Assert.True(workspace.IsDirty);
    }

    [Fact]
    public void DeleteChapter_BlocksWhenOnlyOneRemains()
    {
        var workspace = new WorkspaceViewModel(SampleProject(), new FakeClock(_timestamp));

        workspace.DeleteChapterCommand.Execute(workspace.SelectedChapter);

        Assert.Single(workspace.Chapters);
    }

    private static IReadOnlyList<LanguageData> Catalog() => [new LanguageData("ru", "Russian")];

    private static Project SampleProject() => new()
    {
        Name = "The Ember Crown",
        CreatedUtc = _timestamp,
        UpdatedUtc = _timestamp,
        Settings = new StorySettings { TargetLanguages = ["ru"] },
        Lore = new WorldLore { Title = "Ashen Reach", Body = "A dying empire." },
        Characters =
        [
            new Character { Name = "Aria", Description = "scout", Traits = ["brave"], Goals = "find her brother" },
        ],
        Plot = new PlotDescription { ChapterCount = 1 },
        ExtraFiles =
        [
            new ExtraFile { Name = "bestiary.md", Content = "Wyverns nest in cliffs.", Tags = ["lore"] },
        ],
        WorldState = new WorldState
        {
            TimeAndPlace = "Dusk above the keep",
            Characters = [new CharacterState { Name = "Aria", Status = "unhurt", Location = "cliffs" }],
            ActiveThreads = ["escape"],
            Items = ["relic"],
            OpenQuestions = ["why did the keep fall?"],
        },
        Chapters =
        [
            new Chapter
            {
                Number = 1,
                Title = "Embers",
                ContentOriginal = "Introduction text.",
                Translations = new SortedDictionary<string, string> { ["ru"] = "Дым поднимался." },
                Summary = "Recap.",
                Logline = "Logline.",
                Status = ChapterStatus.Generated,
                CreatedUtc = _timestamp,
            },
        ],
    };
}
