using StoryTelling.Application.Chapters;
using StoryTelling.Domain;

namespace StoryTelling.Tests;

public sealed class ChapterRunnerTests
{
    private static readonly DateTimeOffset _timestamp = DateTimeOffset.UnixEpoch;

    [Fact]
    public async Task GenerateAsync_AppliesResultAndMarksLaterStale()
    {
        var runner = Runner(out _, out _);
        var project = Project();

        await runner.GenerateAsync(project, project.Chapters[0]);

        var first = project.Chapters[0];
        Assert.Equal("Edited.", first.ContentOriginal);
        Assert.Equal("Log.", first.Logline);
        Assert.Equal("Here", first.WorldState!.TimeAndPlace);
        Assert.Equal(ChapterStatus.Generated, first.Status);
        Assert.Equal(ChapterStatus.Stale, project.Chapters[1].Status);
        Assert.Equal(ChapterStatus.Draft, project.Chapters[2].Status);
    }

    [Fact]
    public async Task GenerateAsync_StoresTheRunningStorySoFar()
    {
        var runner = Runner(out _, out var summarizer);
        summarizer.Summary = new ChapterSummary("Log.", new WorldState { TimeAndPlace = "Here" }, [], "A running retelling.");
        var project = Project();

        await runner.GenerateAsync(project, project.Chapters[0]);

        Assert.Equal("A running retelling.", project.Chapters[0].StorySoFar);
    }

    [Fact]
    public async Task GenerateAsync_StoresKnowledgeChangesWithoutMutatingProject()
    {
        var runner = Runner(out _, out var summarizer);
        summarizer.Summary = new ChapterSummary("Log.", new WorldState { TimeAndPlace = "Here" },
        [
            new KnowledgeChange { Operation = KnowledgeChangeOperation.Create, Kind = KnowledgeKind.Character, Title = "Ghost", Content = "A new presence." },
        ]);
        var project = Project();

        await runner.GenerateAsync(project, project.Chapters[0]);

        Assert.Empty(project.Knowledge);
        var change = Assert.Single(project.Chapters[0].KnowledgeChanges);
        Assert.Equal("Ghost", change.Title);
    }

    [Fact]
    public async Task GenerateAsync_PassesComposedKnowledgeFromPriorChapters()
    {
        var runner = Runner(out _, out var summarizer);
        var project = Project();
        project.Knowledge.Add(new KnowledgeEntry { Kind = KnowledgeKind.Character, Title = "Aria", Content = "base" });
        project.Chapters[0].KnowledgeChanges =
        [
            new KnowledgeChange { Operation = KnowledgeChangeOperation.Update, Title = "Aria", Kind = KnowledgeKind.Character, Content = "dead" },
        ];

        await runner.GenerateAsync(project, project.Chapters[1]);

        var aria = Assert.Single(summarizer.LastKnowledge!);
        Assert.Equal("dead", aria.Content);
        Assert.Equal("base", project.Knowledge.Single().Content);
    }

    [Fact]
    public async Task GenerateAsync_CopiesEditorNotesAndMarksTranslationsStale()
    {
        var writer = new FakeChapterAgent { Text = "Draft.", ToolCalls = 1 };
        var editor = new FakeChapterEditor
        {
            Result = "Edited.",
            Notes = [new EditorNote { Kind = EditorNoteKind.Style, Text = "Tightened prose." }],
        };
        var summarizer = new FakeChapterSummarizer { Summary = new ChapterSummary("Log.", new WorldState { TimeAndPlace = "Here" }, []) };
        var runner = new ChapterRunner(new ChapterWorkflow(writer, editor, summarizer, new FakeSettingsService()), summarizer, new FakeClock(_timestamp));
        var project = Project();
        project.Chapters[0].Translations["ru"] = "Дым.";

        await runner.GenerateAsync(project, project.Chapters[0]);

        Assert.Equal("Tightened prose.", Assert.Single(project.Chapters[0].EditorNotes).Text);
        Assert.Contains("ru", project.Chapters[0].StaleTranslations);
    }

    [Fact]
    public async Task GenerateNextAsync_AppendsChapterAndAppliesResult()
    {
        var runner = Runner(out _, out _);
        var project = Project();

        await runner.GenerateNextAsync(project);

        Assert.Equal(4, project.Chapters.Count);
        var added = project.Chapters[^1];
        Assert.Equal(4, added.Number);
        Assert.Equal("Edited.", added.ContentOriginal);
        Assert.Equal(ChapterStatus.Generated, added.Status);
    }

    [Fact]
    public async Task RecomputeFromAsync_RefreshesStaleChaptersAndClearsStale()
    {
        var runner = Runner(out _, out var summarizer);
        var project = Project();
        project.Chapters[1].Status = ChapterStatus.Stale;
        project.Chapters[2].Status = ChapterStatus.Stale;
        project.Chapters[2].ContentOriginal = "text 3";

        await runner.RecomputeFromAsync(project, 2);

        Assert.Equal(ChapterStatus.Generated, project.Chapters[1].Status);
        Assert.Equal(ChapterStatus.Generated, project.Chapters[2].Status);
        Assert.Equal("Log.", project.Chapters[2].Logline);
        Assert.Equal(2, summarizer.CallCount);
    }

    [Fact]
    public async Task RecomputeFromAsync_ReplacesStoredKnowledgeChanges()
    {
        var runner = Runner(out _, out var summarizer);
        summarizer.Summary = new ChapterSummary("Log.", new WorldState { TimeAndPlace = "Here" },
        [
            new KnowledgeChange { Operation = KnowledgeChangeOperation.Create, Kind = KnowledgeKind.Character, Title = "Ghost", Content = "A new presence." },
        ]);
        var project = Project();

        await runner.GenerateAsync(project, project.Chapters[0]);
        Assert.Equal("Ghost", Assert.Single(project.Chapters[0].KnowledgeChanges).Title);

        summarizer.Summary = new ChapterSummary("Log 2.", new WorldState { TimeAndPlace = "Here" },
        [
            new KnowledgeChange { Operation = KnowledgeChangeOperation.Create, Kind = KnowledgeKind.Item, Title = "Relic", Content = "A found artifact." },
        ]);

        await runner.RecomputeFromAsync(project, 1);

        Assert.Equal("Relic", Assert.Single(project.Chapters[0].KnowledgeChanges).Title);
        Assert.Empty(project.Knowledge);
    }

    private static ChapterRunner Runner(out FakeChapterAgent writer, out FakeChapterSummarizer summarizer)
    {
        writer = new FakeChapterAgent { Text = "Draft.", ToolCalls = 2 };
        var editor = new FakeChapterEditor { Result = "Edited." };
        summarizer = new FakeChapterSummarizer { Summary = new ChapterSummary("Log.", new WorldState { TimeAndPlace = "Here" }, []) };
        return new ChapterRunner(new ChapterWorkflow(writer, editor, summarizer, new FakeSettingsService()), summarizer, new FakeClock(_timestamp));
    }

    private static Project Project() => new()
    {
        Name = "Book",
        InitialWorldState = new WorldState { TimeAndPlace = "Start" },
        Chapters =
        [
            new Chapter { Number = 1, Title = "One", ContentOriginal = "text 1", Status = ChapterStatus.Generated },
            new Chapter { Number = 2, Title = "Two", ContentOriginal = "text 2", Status = ChapterStatus.Generated },
            new Chapter { Number = 3, Title = "Three", Status = ChapterStatus.Draft },
        ],
    };
}
