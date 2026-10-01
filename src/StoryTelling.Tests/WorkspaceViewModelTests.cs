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
        var workspace = new WorkspaceViewModel(SampleProject(), new FakeClock(_timestamp), new FakeChapterAgent());

        var chapter = Assert.Single(workspace.ToProject().Chapters);

        Assert.Equal(_timestamp, chapter.CreatedUtc);
        Assert.Equal(1, chapter.Number);
        Assert.Equal("Embers", chapter.Title);
        Assert.Equal("Introduction text.", chapter.ContentOriginal);
        Assert.Equal("Дым поднимался.", chapter.Translations["ru"]);
        Assert.Equal(ChapterStatus.Generated, chapter.Status);
        Assert.Equal("Recap.", chapter.Summary);
        Assert.Equal("Logline.", chapter.Logline);
        Assert.Equal("Dusk", chapter.WorldState!.TimeAndPlace);
    }

    [Fact]
    public void ApplySetup_DoesNotDestroyDomainData()
    {
        var project = SampleProject();
        var workspace = new WorkspaceViewModel(project, new FakeClock(_timestamp), new FakeChapterAgent());
        var setup = workspace.CreateSetup(Catalog(), new DiffPlexTextDiff(), new FakeGenerationAssistant(), new FakeKnowledgeImporter(), new FakeProjectReviewAssistant());

        workspace.ApplySetup(setup);

        Assert.Equal("Wyverns nest in cliffs.", project.Knowledge.Single().Content);
        Assert.Contains("lore", project.Knowledge.Single().Tags);
        Assert.Contains("brave", project.Characters.Single().Traits);
        Assert.Equal("Dusk above the keep", project.WorldState.TimeAndPlace);
        Assert.Equal("Aria crouches in the ruins.", project.WorldState.Description);
    }

    [Fact]
    public void AddChapter_RenumbersAndMarksDirty()
    {
        var workspace = new WorkspaceViewModel(SampleProject(), new FakeClock(_timestamp), new FakeChapterAgent());
        workspace.IsDirty = false;

        workspace.AddChapterCommand.Execute(null);

        Assert.Equal(2, workspace.Chapters.Count);
        Assert.Equal(new[] { 1, 2 }, workspace.Chapters.Select(chapter => chapter.Number));
        Assert.True(workspace.IsDirty);
    }

    [Fact]
    public void DeleteChapter_BlocksWhenOnlyOneRemains()
    {
        var workspace = new WorkspaceViewModel(SampleProject(), new FakeClock(_timestamp), new FakeChapterAgent());

        workspace.DeleteChapterCommand.Execute(workspace.SelectedChapter);

        Assert.Single(workspace.Chapters);
    }

    [Fact]
    public async Task Generate_WritesDraftIntoChapterAndMarksGenerated()
    {
        var agent = new FakeChapterAgent { Text = "Aria stepped into the dark." };
        var workspace = new WorkspaceViewModel(SampleProject(), new FakeClock(_timestamp), agent);
        var chapter = workspace.SelectedChapter;
        chapter.Status = ChapterStatus.Draft;
        chapter.ContentOriginal = string.Empty;

        await workspace.GenerateCommand.ExecuteAsync(null);

        Assert.Equal("Aria stepped into the dark.", chapter.ContentOriginal);
        Assert.Equal(ChapterStatus.Generated, chapter.Status);
        Assert.NotNull(agent.LastContext);
        Assert.Contains("Dusk above the keep", agent.LastContext!.StateBefore.TimeAndPlace);
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
        Frame = new StoryFrame(),
        Knowledge =
        [
            new KnowledgeEntry { Kind = KnowledgeKind.Note, Title = "bestiary.md", Content = "Wyverns nest in cliffs.", Tags = ["lore"] },
        ],
        WorldState = new WorldState
        {
            TimeAndPlace = "Dusk above the keep",
            Description = "Aria crouches in the ruins.",
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
                WorldState = new WorldState { TimeAndPlace = "Dusk" },
                Status = ChapterStatus.Generated,
                CreatedUtc = _timestamp,
            },
        ],
    };
}
