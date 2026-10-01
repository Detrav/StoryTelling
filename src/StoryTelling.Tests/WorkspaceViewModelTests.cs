using System.Linq;
using StoryTelling.Application.Chapters;
using StoryTelling.Application.Generation;
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
        var workspace = new WorkspaceViewModel(SampleProject(), new FakeClock(_timestamp), new FakeChapterRunner(), new FakeGenerationAssistant(), new FakeTranslationService());

        var chapter = Assert.Single(workspace.ToProject().Chapters);

        Assert.Equal(_timestamp, chapter.CreatedUtc);
        Assert.Equal(1, chapter.Number);
        Assert.Equal("Embers", chapter.Title);
        Assert.Equal("Introduction text.", chapter.ContentOriginal);
        Assert.Equal("Дым поднимался.", chapter.Translations["ru"]);
        Assert.Equal(ChapterStatus.Generated, chapter.Status);
        Assert.Equal("Logline.", chapter.Logline);
        Assert.Equal("Dusk", chapter.WorldState!.TimeAndPlace);
    }

    [Fact]
    public void ApplySetup_DoesNotDestroyDomainData()
    {
        var project = SampleProject();
        var workspace = new WorkspaceViewModel(project, new FakeClock(_timestamp), new FakeChapterRunner(), new FakeGenerationAssistant(), new FakeTranslationService());
        var setup = workspace.CreateSetup(Catalog(), new DiffPlexTextDiff(), new FakeGenerationAssistant(), new FakeKnowledgeImporter(), new FakeProjectReviewAssistant());

        workspace.ApplySetup(setup);

        Assert.Equal("Wyverns nest in cliffs.", project.Knowledge.Single().Content);
        Assert.Contains("lore", project.Knowledge.Single().Tags);
        Assert.Equal("Dusk above the keep", project.InitialWorldState.TimeAndPlace);
        Assert.Equal("Aria crouches in the ruins.", project.InitialWorldState.Description);
    }

    [Fact]
    public async Task TranslateChapter_UpdatesTranslationAndClearsStale()
    {
        var project = SampleProject();
        project.Chapters[0].StaleTranslations = ["ru"];
        var translation = new FakeTranslationService { Result = "Дым обновлён." };
        var workspace = new WorkspaceViewModel(project, new FakeClock(_timestamp), new FakeChapterRunner(), new FakeGenerationAssistant(), translation);

        await workspace.TranslateChapterCommand.ExecuteAsync(null);

        Assert.Equal("Дым обновлён.", workspace.SelectedChapter.Translations.Single().Text);
        Assert.Empty(workspace.SelectedChapter.StaleTranslations);
        Assert.Equal("ru", translation.LastLanguageCode);
    }

    [Fact]
    public async Task Generate_PopulatesSummaryKnowledgeAndEditorNotes()
    {
        var runner = new FakeChapterRunner
        {
            KnowledgeChanges = [new KnowledgeChange { Operation = KnowledgeChangeOperation.Update, Kind = KnowledgeKind.Character, Title = "Mira Vale", Content = "dead" }],
            EditorNotes = [new EditorNote { Kind = EditorNoteKind.Continuity, Text = "Fixed the timeline." }],
        };
        var workspace = new WorkspaceViewModel(SampleProject(), new FakeClock(_timestamp), runner, new FakeGenerationAssistant(), new FakeTranslationService());
        var summary = (ChapterSummaryViewModel)workspace.SelectedChapter.Tabs.Single(tab => tab.Content is ChapterSummaryViewModel).Content;
        workspace.SelectedChapter.Direction = "Advance.";

        Assert.Empty(summary.Changes);

        await workspace.GenerateCommand.ExecuteAsync(null);

        var change = Assert.Single(summary.Changes);
        Assert.Equal("Mira Vale", change.Title);
        Assert.Equal(KnowledgeChangeOperation.Update, change.Operation);
        Assert.Equal("Fixed the timeline.", Assert.Single(summary.EditorNotes).Text);

        var saved = workspace.ToProject().Chapters.Single();
        Assert.Single(saved.KnowledgeChanges);
        Assert.Single(saved.EditorNotes);
    }

    [Fact]
    public async Task RegenerateSummary_UpdatesChapterFromRunner()
    {
        var runner = new FakeChapterRunner
        {
            Summary = new ChapterSummary("New logline.", new WorldState { TimeAndPlace = "New place" },
            [
                new KnowledgeChange { Operation = KnowledgeChangeOperation.Create, Kind = KnowledgeKind.Item, Title = "Relic", Content = "x" },
            ]),
        };
        var workspace = new WorkspaceViewModel(SampleProject(), new FakeClock(_timestamp), runner, new FakeGenerationAssistant(), new FakeTranslationService());
        var chapter = workspace.SelectedChapter;
        var summary = (ChapterSummaryViewModel)chapter.Tabs.Single(tab => tab.Content is ChapterSummaryViewModel).Content;

        await summary.RegenerateSummaryCommand.ExecuteAsync(null);

        Assert.Equal("New logline.", chapter.Logline);
        Assert.Equal("New place", chapter.WorldState!.TimeAndPlace);
        Assert.Equal("Relic", Assert.Single(summary.Changes).Title);
    }

    [Fact]
    public void ApplySetup_WorldChange_MarksChaptersStale()
    {
        var project = SampleProject();
        var workspace = new WorkspaceViewModel(project, new FakeClock(_timestamp), new FakeChapterRunner(), new FakeGenerationAssistant(), new FakeTranslationService());
        var setup = workspace.CreateSetup(Catalog(), new DiffPlexTextDiff(), new FakeGenerationAssistant(), new FakeKnowledgeImporter(), new FakeProjectReviewAssistant());

        setup.WorldBody = "A changed world.";

        workspace.ApplySetup(setup);

        Assert.Equal(ChapterStatus.Stale, workspace.SelectedChapter.Status);
    }

    [Fact]
    public void ApplySetup_NoChange_KeepsChaptersGenerated()
    {
        var project = SampleProject();
        var workspace = new WorkspaceViewModel(project, new FakeClock(_timestamp), new FakeChapterRunner(), new FakeGenerationAssistant(), new FakeTranslationService());
        var setup = workspace.CreateSetup(Catalog(), new DiffPlexTextDiff(), new FakeGenerationAssistant(), new FakeKnowledgeImporter(), new FakeProjectReviewAssistant());

        workspace.ApplySetup(setup);

        Assert.Equal(ChapterStatus.Generated, workspace.SelectedChapter.Status);
    }

    [Fact]
    public void EditSummaryKnowledge_MarksLaterChaptersStale()
    {
        var project = SampleProject();
        var workspace = new WorkspaceViewModel(project, new FakeClock(_timestamp), new FakeChapterRunner(), new FakeGenerationAssistant(), new FakeTranslationService());
        workspace.AddChapterCommand.Execute(null);
        workspace.Chapters[1].Status = ChapterStatus.Generated;
        workspace.SelectedChapter = workspace.Chapters[0];
        var summary = (ChapterSummaryViewModel)workspace.Chapters[0].Tabs.Single(tab => tab.Content is ChapterSummaryViewModel).Content;

        summary.AddChange(new KnowledgeChangeEditorViewModel { Operation = KnowledgeChangeOperation.Create, Title = "Relic" });
        summary.Commit();

        Assert.Equal(ChapterStatus.Stale, workspace.Chapters[1].Status);
    }

    [Fact]
    public async Task Generate_EmptySetup_WarnsWithDetails()
    {
        var project = new Project
        {
            World = new World(),
            Chapters = [new Chapter { Number = 1, Title = "One", Direction = "Go.", Status = ChapterStatus.Draft }],
        };
        var runner = new FakeChapterRunner();
        var workspace = new WorkspaceViewModel(project, new FakeClock(_timestamp), runner, new FakeGenerationAssistant(), new FakeTranslationService());
        string? warning = null;
        workspace.WarningRequested += (_, message) => warning = message;

        await workspace.GenerateCommand.ExecuteAsync(null);

        Assert.NotNull(warning);
        Assert.Contains("world", warning);
        Assert.Contains("frame", warning);
        Assert.Contains("initial world state", warning);
        Assert.Null(runner.LastStateBefore);
    }

    [Fact]
    public void ApplyChapterPlan_ReplacesChapters()
    {
        var workspace = new WorkspaceViewModel(SampleProject(), new FakeClock(_timestamp), new FakeChapterRunner(), new FakeGenerationAssistant(), new FakeTranslationService());
        workspace.IsDirty = false;

        workspace.ApplyChapterPlan([("Embers", "Open quietly."), ("Ash", "Raise the stakes.")]);

        Assert.Equal(2, workspace.Chapters.Count);
        Assert.Equal("Embers", workspace.Chapters[0].Title);
        Assert.Equal("Open quietly.", workspace.Chapters[0].Direction);
        Assert.Equal(ChapterStatus.Draft, workspace.Chapters[0].Status);
        Assert.Same(workspace.Chapters[0], workspace.SelectedChapter);
        Assert.True(workspace.IsDirty);
    }

    [Fact]
    public async Task PlanChaptersAsync_UsesChapterPlanTarget()
    {
        var assistant = new FakeGenerationAssistant();
        var workspace = new WorkspaceViewModel(SampleProject(), new FakeClock(_timestamp), new FakeChapterRunner(), assistant, new FakeTranslationService());

        await workspace.PlanChaptersAsync(3, "dark fantasy", new GenerationSession(), null, CancellationToken.None);

        Assert.NotNull(assistant.LastRequest);
        Assert.Equal(GenerationTarget.ChapterPlan, assistant.LastRequest!.Target);
        Assert.Equal(3, assistant.LastRequest.Variants);
        Assert.Empty(assistant.LastRequest.Snapshot!.Chapters);
    }

    [Fact]
    public async Task FinishStory_AddsChapterWithPlannedFinale()
    {
        var assistant = new FakeGenerationAssistant
        {
            Options = [new GenerationOption(new Dictionary<string, string> { ["Title"] = "The End", ["Direction"] = "Everything resolves." })],
        };
        var workspace = new WorkspaceViewModel(SampleProject(), new FakeClock(_timestamp), new FakeChapterRunner(), assistant, new FakeTranslationService());
        var before = workspace.Chapters.Count;

        await workspace.FinishStoryCommand.ExecuteAsync(null);

        Assert.Equal(before + 1, workspace.Chapters.Count);
        var last = workspace.Chapters[^1];
        Assert.Equal("The End", last.Title);
        Assert.Equal("Everything resolves.", last.Direction);
        Assert.Same(last, workspace.SelectedChapter);
        Assert.Equal(GenerationTarget.Finale, assistant.LastRequest!.Target);
    }

    [Fact]
    public void AddChapter_RenumbersAndMarksDirty()
    {
        var workspace = new WorkspaceViewModel(SampleProject(), new FakeClock(_timestamp), new FakeChapterRunner(), new FakeGenerationAssistant(), new FakeTranslationService());
        workspace.IsDirty = false;

        workspace.AddChapterCommand.Execute(null);

        Assert.Equal(2, workspace.Chapters.Count);
        Assert.Equal(new[] { 1, 2 }, workspace.Chapters.Select(chapter => chapter.Number));
        Assert.True(workspace.IsDirty);
    }

    [Fact]
    public void DeleteChapter_BlocksWhenOnlyOneRemains()
    {
        var workspace = new WorkspaceViewModel(SampleProject(), new FakeClock(_timestamp), new FakeChapterRunner(), new FakeGenerationAssistant(), new FakeTranslationService());

        workspace.DeleteChapterCommand.Execute(workspace.SelectedChapter);

        Assert.Single(workspace.Chapters);
    }

    [Fact]
    public async Task Generate_WritesDraftIntoChapterAndMarksGenerated()
    {
        var agent = new FakeChapterRunner { Text = "Aria stepped into the dark." };
        var workspace = new WorkspaceViewModel(SampleProject(), new FakeClock(_timestamp), agent, new FakeGenerationAssistant(), new FakeTranslationService());
        var chapter = workspace.SelectedChapter;
        chapter.Status = ChapterStatus.Draft;
        chapter.ContentOriginal = string.Empty;
        chapter.Direction = "Advance.";

        await workspace.GenerateCommand.ExecuteAsync(null);

        Assert.Equal("Aria stepped into the dark.", chapter.ContentOriginal);
        Assert.Equal(ChapterStatus.Generated, chapter.Status);
        Assert.NotNull(agent.LastStateBefore);
        Assert.Contains("Dusk above the keep", agent.LastStateBefore!.TimeAndPlace);
    }

    [Fact]
    public async Task Generate_EmptyDirection_WarnsAndDoesNotRun()
    {
        var runner = new FakeChapterRunner { Text = "New draft." };
        var workspace = new WorkspaceViewModel(SampleProject(), new FakeClock(_timestamp), runner, new FakeGenerationAssistant(), new FakeTranslationService());
        var chapter = workspace.SelectedChapter;
        chapter.Direction = string.Empty;
        string? warning = null;
        workspace.WarningRequested += (_, message) => warning = message;

        await workspace.GenerateCommand.ExecuteAsync(null);

        Assert.NotNull(warning);
        Assert.Null(runner.LastStateBefore);
        Assert.Equal("Introduction text.", chapter.ContentOriginal);
    }

    private static IReadOnlyList<LanguageData> Catalog() => [new LanguageData("ru", "Russian")];

    private static Project SampleProject() => new()
    {
        Name = "The Ember Crown",
        CreatedUtc = _timestamp,
        UpdatedUtc = _timestamp,
        Settings = new StorySettings { TargetLanguages = ["ru"] },
        World = new World
        {
            Title = "Ashen Reach",
            Body = "A dying empire.",
            Genre = "fantasy",
            Tone = "grim",
            Style = "terse",
            PointOfView = "third person",
            Tense = "past",
            Rating = "PG-13",
        },
        Knowledge =
        [
            new KnowledgeEntry { Kind = KnowledgeKind.Note, Title = "bestiary.md", Content = "Wyverns nest in cliffs.", Tags = ["lore"] },
        ],
        InitialWorldState = new WorldState
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
                Logline = "Logline.",
                WorldState = new WorldState { TimeAndPlace = "Dusk" },
                Status = ChapterStatus.Generated,
                CreatedUtc = _timestamp,
            },
        ],
    };
}
