using System.Linq;
using StoryTelling.Application.Chapters;
using StoryTelling.Application.Generation;
using StoryTelling.Application.Settings;
using StoryTelling.Application.Translation;
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
        var workspace = new WorkspaceViewModel(SampleProject(), new FakeClock(_timestamp), new FakeChapterRunner(), new FakeGenerationAssistant(), new FakeTranslationService(), new FakeMetadataTranslator());

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
        var workspace = new WorkspaceViewModel(project, new FakeClock(_timestamp), new FakeChapterRunner(), new FakeGenerationAssistant(), new FakeTranslationService(), new FakeMetadataTranslator());
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
        var workspace = new WorkspaceViewModel(project, new FakeClock(_timestamp), new FakeChapterRunner(), new FakeGenerationAssistant(), translation, new FakeMetadataTranslator());

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
        var workspace = new WorkspaceViewModel(SampleProject(), new FakeClock(_timestamp), runner, new FakeGenerationAssistant(), new FakeTranslationService(), new FakeMetadataTranslator());
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
        var workspace = new WorkspaceViewModel(SampleProject(), new FakeClock(_timestamp), runner, new FakeGenerationAssistant(), new FakeTranslationService(), new FakeMetadataTranslator());
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
        var workspace = new WorkspaceViewModel(project, new FakeClock(_timestamp), new FakeChapterRunner(), new FakeGenerationAssistant(), new FakeTranslationService(), new FakeMetadataTranslator());
        var setup = workspace.CreateSetup(Catalog(), new DiffPlexTextDiff(), new FakeGenerationAssistant(), new FakeKnowledgeImporter(), new FakeProjectReviewAssistant());

        setup.WorldBody = "A changed world.";

        workspace.ApplySetup(setup);

        Assert.Equal(ChapterStatus.Stale, workspace.SelectedChapter.Status);
    }

    [Fact]
    public void ApplySetup_NoChange_KeepsChaptersGenerated()
    {
        var project = SampleProject();
        var workspace = new WorkspaceViewModel(project, new FakeClock(_timestamp), new FakeChapterRunner(), new FakeGenerationAssistant(), new FakeTranslationService(), new FakeMetadataTranslator());
        var setup = workspace.CreateSetup(Catalog(), new DiffPlexTextDiff(), new FakeGenerationAssistant(), new FakeKnowledgeImporter(), new FakeProjectReviewAssistant());

        workspace.ApplySetup(setup);

        Assert.Equal(ChapterStatus.Generated, workspace.SelectedChapter.Status);
    }

    [Fact]
    public void EditSummaryKnowledge_MarksLaterChaptersStale()
    {
        var project = SampleProject();
        var workspace = new WorkspaceViewModel(project, new FakeClock(_timestamp), new FakeChapterRunner(), new FakeGenerationAssistant(), new FakeTranslationService(), new FakeMetadataTranslator());
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
        var workspace = new WorkspaceViewModel(project, new FakeClock(_timestamp), runner, new FakeGenerationAssistant(), new FakeTranslationService(), new FakeMetadataTranslator());
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
        var workspace = new WorkspaceViewModel(SampleProject(), new FakeClock(_timestamp), new FakeChapterRunner(), new FakeGenerationAssistant(), new FakeTranslationService(), new FakeMetadataTranslator());
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
        var workspace = new WorkspaceViewModel(SampleProject(), new FakeClock(_timestamp), new FakeChapterRunner(), assistant, new FakeTranslationService(), new FakeMetadataTranslator());

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
        var workspace = new WorkspaceViewModel(SampleProject(), new FakeClock(_timestamp), new FakeChapterRunner(), assistant, new FakeTranslationService(), new FakeMetadataTranslator());
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
        var workspace = new WorkspaceViewModel(SampleProject(), new FakeClock(_timestamp), new FakeChapterRunner(), new FakeGenerationAssistant(), new FakeTranslationService(), new FakeMetadataTranslator());
        workspace.IsDirty = false;

        workspace.AddChapterCommand.Execute(null);

        Assert.Equal(2, workspace.Chapters.Count);
        Assert.Equal(new[] { 1, 2 }, workspace.Chapters.Select(chapter => chapter.Number));
        Assert.True(workspace.IsDirty);
    }

    [Fact]
    public void DeleteChapter_BlocksWhenOnlyOneRemains()
    {
        var workspace = new WorkspaceViewModel(SampleProject(), new FakeClock(_timestamp), new FakeChapterRunner(), new FakeGenerationAssistant(), new FakeTranslationService(), new FakeMetadataTranslator());

        workspace.DeleteChapterCommand.Execute(workspace.SelectedChapter);

        Assert.Single(workspace.Chapters);
    }

    [Fact]
    public async Task Generate_WritesDraftIntoChapterAndMarksGenerated()
    {
        var agent = new FakeChapterRunner { Text = "Aria stepped into the dark." };
        var workspace = new WorkspaceViewModel(SampleProject(), new FakeClock(_timestamp), agent, new FakeGenerationAssistant(), new FakeTranslationService(), new FakeMetadataTranslator());
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
        var workspace = new WorkspaceViewModel(SampleProject(), new FakeClock(_timestamp), runner, new FakeGenerationAssistant(), new FakeTranslationService(), new FakeMetadataTranslator());
        var chapter = workspace.SelectedChapter;
        chapter.Direction = string.Empty;
        string? warning = null;
        workspace.WarningRequested += (_, message) => warning = message;

        await workspace.GenerateCommand.ExecuteAsync(null);

        Assert.NotNull(warning);
        Assert.Null(runner.LastStateBefore);
        Assert.Equal("Introduction text.", chapter.ContentOriginal);
    }

    [Fact]
    public async Task TranslateMetadata_AppliesResultAndClearsStale()
    {
        var project = SampleProject();
        project.StaleMetadataTranslations = ["ru"];
        var metadata = new FakeMetadataTranslator
        {
            Result = new MetadataTranslationResult("Корона", "Аннотация", new Dictionary<int, string> { [1] = "Угли" }),
        };
        var workspace = new WorkspaceViewModel(project, new FakeClock(_timestamp), new FakeChapterRunner(), new FakeGenerationAssistant(), new FakeTranslationService(), metadata);

        await workspace.TranslateMetadataAsync(new Progress<MetadataTranslationProgress>(_ => { }), CancellationToken.None);

        Assert.Equal("Корона", project.MetadataTranslations["ru"].Name);
        Assert.Equal("Аннотация", project.MetadataTranslations["ru"].Annotation);
        Assert.Equal("Угли", workspace.ToProject().Chapters[0].TranslatedTitles["ru"]);
        Assert.Empty(project.StaleMetadataTranslations);
        Assert.Equal("ru", metadata.LastRequest!.LanguageCode);
    }

    [Fact]
    public void MetadataLanguages_ExcludesEnglish()
    {
        var project = SampleProject();
        project.Settings.TargetLanguages = ["ru", "en", "de"];
        var workspace = new WorkspaceViewModel(project, new FakeClock(_timestamp), new FakeChapterRunner(), new FakeGenerationAssistant(), new FakeTranslationService(), new FakeMetadataTranslator());

        Assert.Equal(new[] { "ru", "de" }, workspace.MetadataLanguages);
    }

    [Fact]
    public void AddChapter_MarksMetadataStale()
    {
        var project = SampleProject();
        project.MetadataTranslations["ru"] = new MetadataTranslation { Name = "Корона", Annotation = "Аннотация" };
        var workspace = new WorkspaceViewModel(project, new FakeClock(_timestamp), new FakeChapterRunner(), new FakeGenerationAssistant(), new FakeTranslationService(), new FakeMetadataTranslator());

        workspace.AddChapterCommand.Execute(null);

        Assert.Contains("ru", workspace.ToProject().StaleMetadataTranslations);
    }

    [Fact]
    public async Task TranslateMetadata_SkipsCompleteLanguages()
    {
        var project = TranslatedSampleProject();
        var metadata = new FakeMetadataTranslator();
        var workspace = new WorkspaceViewModel(project, new FakeClock(_timestamp), new FakeChapterRunner(), new FakeGenerationAssistant(), new FakeTranslationService(), metadata);

        var reports = new List<MetadataTranslationProgress>();
        await workspace.TranslateMetadataAsync(new SynchronousProgress<MetadataTranslationProgress>(reports.Add), CancellationToken.None);

        Assert.Empty(metadata.Calls);
        Assert.Equal("Book metadata translated.", workspace.Status);
        Assert.Equal("Угли", workspace.SelectedChapter.TranslatedTitles["ru"]);
    }

    [Fact]
    public async Task TranslateMetadata_ReportsProgressPerLanguage()
    {
        var project = SampleProject();
        project.Settings.TargetLanguages = ["ru", "de"];
        var metadata = new FakeMetadataTranslator
        {
            ResultFactory = request => new MetadataTranslationResult(
                $"Name {request.LanguageCode}",
                "Аннотация",
                new Dictionary<int, string> { [1] = $"Title {request.LanguageCode}" }),
        };
        var workspace = new WorkspaceViewModel(project, new FakeClock(_timestamp), new FakeChapterRunner(), new FakeGenerationAssistant(), new FakeTranslationService(), metadata);

        var reports = new List<MetadataTranslationProgress>();
        await workspace.TranslateMetadataAsync(new SynchronousProgress<MetadataTranslationProgress>(reports.Add), CancellationToken.None);

        Assert.Equal(["ru", "de"], metadata.Calls);
        Assert.Equal(2, reports[^1].Total);
        Assert.Equal(2, reports[^1].Completed);
        Assert.Equal("Name de", workspace.ToProject().MetadataTranslations["de"].Name);
    }

    [Fact]
    public async Task TranslateMetadata_MarksDirtyAndMutatesOnce()
    {
        var project = SampleProject();
        var workspace = new WorkspaceViewModel(project, new FakeClock(_timestamp), new FakeChapterRunner(), new FakeGenerationAssistant(), new FakeTranslationService(), new FakeMetadataTranslator());
        var mutations = new List<string>();
        workspace.Mutated += name => mutations.Add(name);
        workspace.IsDirty = false;

        await workspace.TranslateMetadataAsync(new SynchronousProgress<MetadataTranslationProgress>(_ => { }), CancellationToken.None);

        Assert.True(workspace.IsDirty);
        Assert.Equal(["Translate book metadata"], mutations);
    }

    [Fact]
    public async Task TranslateMetadata_WhenNothingApplied_LeavesProjectClean()
    {
        var workspace = new WorkspaceViewModel(SampleProject(), new FakeClock(_timestamp), new FakeChapterRunner(), new FakeGenerationAssistant(), new FakeTranslationService(), new FakeMetadataTranslator { Failure = new InvalidOperationException("busy") });
        var mutations = new List<string>();
        workspace.Mutated += name => mutations.Add(name);
        workspace.IsDirty = false;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => workspace.TranslateMetadataAsync(new SynchronousProgress<MetadataTranslationProgress>(_ => { }), CancellationToken.None));

        Assert.False(workspace.IsDirty);
        Assert.Empty(mutations);
        Assert.False(workspace.IsBusy);
    }

    [Fact]
    public async Task TranslateMetadata_ResultWithoutChapter_RemovesStaleTranslatedTitle()
    {
        var project = TranslatedSampleProject();
        project.StaleMetadataTranslations = ["ru"];
        var metadata = new FakeMetadataTranslator { Result = new MetadataTranslationResult("Новая книга", "Аннотация", new Dictionary<int, string>()) };
        var workspace = new WorkspaceViewModel(project, new FakeClock(_timestamp), new FakeChapterRunner(), new FakeGenerationAssistant(), new FakeTranslationService(), metadata);

        await workspace.TranslateMetadataAsync(new SynchronousProgress<MetadataTranslationProgress>(_ => { }), CancellationToken.None);

        Assert.Empty(workspace.ToProject().Chapters[0].TranslatedTitles);
        Assert.Empty(project.StaleMetadataTranslations);
    }

    [Fact]
    public void ChapterTitles_SkipsBlankTitlesAndTrims()
    {
        var project = SampleProject();
        project.Chapters.Add(new Chapter { Number = 2, Title = "   ", ContentOriginal = "Two." });
        var workspace = new WorkspaceViewModel(project, new FakeClock(_timestamp), new FakeChapterRunner(), new FakeGenerationAssistant(), new FakeTranslationService(), new FakeMetadataTranslator());

        var titles = workspace.ChapterTitles();

        Assert.Equal("Embers", Assert.Single(titles).Title);
    }

    [Fact]
    public void DeleteChapter_MarksMetadataStale()
    {
        var project = TranslatedSampleProject();
        project.Chapters.Add(new Chapter { Number = 2, Title = "Ash", ContentOriginal = "Two." });
        var workspace = new WorkspaceViewModel(project, new FakeClock(_timestamp), new FakeChapterRunner(), new FakeGenerationAssistant(), new FakeTranslationService(), new FakeMetadataTranslator());

        workspace.DeleteChapterCommand.Execute(workspace.Chapters[1]);

        Assert.Contains("ru", workspace.ToProject().StaleMetadataTranslations);
    }

    [Fact]
    public void ApplyChapterPlan_MarksMetadataStale()
    {
        var project = TranslatedSampleProject();
        var workspace = new WorkspaceViewModel(project, new FakeClock(_timestamp), new FakeChapterRunner(), new FakeGenerationAssistant(), new FakeTranslationService(), new FakeMetadataTranslator());

        workspace.ApplyChapterPlan([("Embers", "Open quietly.")]);

        Assert.Contains("ru", workspace.ToProject().StaleMetadataTranslations);
    }

    [Fact]
    public void ChapterTitleEdit_MarksMetadataStale_ButDirectionEditDoesNot()
    {
        var project = TranslatedSampleProject();
        var workspace = new WorkspaceViewModel(project, new FakeClock(_timestamp), new FakeChapterRunner(), new FakeGenerationAssistant(), new FakeTranslationService(), new FakeMetadataTranslator());
        var settings = (ChapterSettingsViewModel)workspace.SelectedChapter.Tabs.Single(tab => tab.Content is ChapterSettingsViewModel).Content;

        settings.Direction = "Advance quietly.";
        settings.Commit();

        Assert.DoesNotContain("ru", workspace.ToProject().StaleMetadataTranslations);

        settings.Title = "Embers and Ash";
        settings.Commit();

        Assert.Contains("ru", workspace.ToProject().StaleMetadataTranslations);
    }

    [Fact]
    public void ApplySetup_RenamingTheBook_MarksMetadataStale_ButUnrelatedEditsDoNot()
    {
        var project = TranslatedSampleProject();
        var workspace = new WorkspaceViewModel(project, new FakeClock(_timestamp), new FakeChapterRunner(), new FakeGenerationAssistant(), new FakeTranslationService(), new FakeMetadataTranslator());
        var setup = workspace.CreateSetup(Catalog(), new DiffPlexTextDiff(), new FakeGenerationAssistant(), new FakeKnowledgeImporter(), new FakeProjectReviewAssistant());

        setup.Genre = "dark fantasy";
        workspace.ApplySetup(setup);

        Assert.DoesNotContain("ru", workspace.ToProject().StaleMetadataTranslations);

        setup.WorldBody = "A different empire.";
        workspace.ApplySetup(setup);

        Assert.Contains("ru", workspace.ToProject().StaleMetadataTranslations);
    }

    [Fact]
    public void ApplySetup_RemovingALanguage_PrunesItsMetadata()
    {
        var project = TranslatedSampleProject();
        project.Settings.TargetLanguages = ["ru", "de"];
        project.MetadataTranslations["de"] = new MetadataTranslation { Name = "Krone DE", Annotation = "Anno DE" };
        project.StaleMetadataTranslations = ["de"];
        project.Chapters[0].TranslatedTitles["de"] = "Funken";
        var workspace = new WorkspaceViewModel(project, new FakeClock(_timestamp), new FakeChapterRunner(), new FakeGenerationAssistant(), new FakeTranslationService(), new FakeMetadataTranslator());
        var setup = workspace.CreateSetup(Catalog(), new DiffPlexTextDiff(), new FakeGenerationAssistant(), new FakeKnowledgeImporter(), new FakeProjectReviewAssistant());
        foreach (var selection in setup.LanguageSelections)
        {
            selection.IsSelected = false;
        }

        setup.LanguageSelections.Single(selection => selection.Language.Code == "ru").IsSelected = true;

        workspace.ApplySetup(setup);

        var saved = workspace.ToProject();
        Assert.Equal(["ru"], saved.MetadataTranslations.Keys);
        Assert.Empty(saved.StaleMetadataTranslations);
        Assert.Equal(["ru"], saved.Chapters[0].TranslatedTitles.Keys);
    }

    [Fact]
    public void ToProject_RoundTripsTranslatedTitles()
    {
        var project = TranslatedSampleProject();
        var workspace = new WorkspaceViewModel(project, new FakeClock(_timestamp), new FakeChapterRunner(), new FakeGenerationAssistant(), new FakeTranslationService(), new FakeMetadataTranslator());

        var reopened = new WorkspaceViewModel(workspace.ToProject(), new FakeClock(_timestamp), new FakeChapterRunner(), new FakeGenerationAssistant(), new FakeTranslationService(), new FakeMetadataTranslator());

        Assert.Equal("Угли", reopened.Chapters[0].TranslatedTitles["ru"]);
    }

    private static Project TranslatedSampleProject()
    {
        var project = SampleProject();
        project.MetadataTranslations["ru"] = new MetadataTranslation { Name = "Книга", Annotation = "Аннотация" };
        project.Chapters[0].TranslatedTitles["ru"] = "Угли";
        return project;
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
