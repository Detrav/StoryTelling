using StoryTelling.Application.Generation;
using StoryTelling.Application.Review;
using StoryTelling.Domain;
using StoryTelling.Infrastructure.Diff;
using StoryTelling.ViewModels;

namespace StoryTelling.Tests;

public sealed class SetupViewModelTests
{
    [Fact]
    public void ApplyGenerated_SetsGroupedFields()
    {
        var setup = new SetupViewModel(new DiffPlexTextDiff(), [], [], new FakeGenerationAssistant(), new FakeKnowledgeImporter(), new FakeProjectReviewAssistant());

        setup.ApplyGenerated(new Dictionary<string, string>
        {
            ["WorldTitle"] = "Ashen",
            ["WorldBody"] = "A cold land.",
        });

        Assert.Equal("Ashen", setup.WorldTitle);
        Assert.Equal("A cold land.", setup.WorldBody);
    }

    [Fact]
    public void ApplyGenerated_SetsStyleFields()
    {
        var setup = new SetupViewModel(new DiffPlexTextDiff(), [], [], new FakeGenerationAssistant(), new FakeKnowledgeImporter(), new FakeProjectReviewAssistant());

        setup.ApplyGenerated(new Dictionary<string, string>
        {
            ["Genre"] = "dark fantasy",
            ["Tone"] = "grim",
        });

        Assert.Equal("dark fantasy", setup.Genre);
        Assert.Equal("grim", setup.Tone);
    }

    [Fact]
    public void ApplyGenerated_SetsInitialWorldStateFields()
    {
        var setup = new SetupViewModel(new DiffPlexTextDiff(), [], [], new FakeGenerationAssistant(), new FakeKnowledgeImporter(), new FakeProjectReviewAssistant());

        setup.ApplyGenerated(new Dictionary<string, string>
        {
            ["TimeAndPlace"] = "Dusk above the keep",
            ["Description"] = "Aria crouches in the ruins.",
        });

        Assert.Equal("Dusk above the keep", setup.InitialStateTimeAndPlace);
        Assert.Equal("Aria crouches in the ruins.", setup.InitialStateDescription);
    }

    [Fact]
    public void Knowledge_AddEditRemove_UpdatesCollection()
    {
        var setup = new SetupViewModel(new DiffPlexTextDiff(), [], [], new FakeGenerationAssistant(), new FakeKnowledgeImporter(), new FakeProjectReviewAssistant());
        var entry = new KnowledgeEntryEditorViewModel
        {
            Kind = KnowledgeKind.Place,
            Title = "Ashen Reach",
            Tags = "region, ash",
            Content = "A frozen frontier.",
        };

        setup.AddKnowledge(entry);
        Assert.Same(entry, setup.SelectedKnowledge);

        var draft = entry.Clone();
        draft.Title = "Ashen Reach (updated)";
        setup.ApplyKnowledgeEdit(entry, draft);
        Assert.Equal("Ashen Reach (updated)", entry.Title);

        setup.RemoveKnowledge(entry);
        Assert.Empty(setup.Knowledge);
        Assert.Null(setup.SelectedKnowledge);
    }

    [Fact]
    public void AddKnowledgeRange_SkipsExistingTitles()
    {
        var setup = new SetupViewModel(new DiffPlexTextDiff(), [], [], new FakeGenerationAssistant(), new FakeKnowledgeImporter(), new FakeProjectReviewAssistant());
        setup.AddKnowledge(new KnowledgeEntryEditorViewModel { Title = "Ashen Reach" });

        setup.AddKnowledgeRange(
        [
            new KnowledgeEntryEditorViewModel { Title = "Ashen Reach" },
            new KnowledgeEntryEditorViewModel { Title = "Frost Keep" },
        ]);

        Assert.Equal(2, setup.Knowledge.Count);
        Assert.Single(setup.Knowledge, entry => entry.Title == "Ashen Reach");
        Assert.Single(setup.Knowledge, entry => entry.Title == "Frost Keep");
    }

    [Fact]
    public void MapTarget_MapsWorldAndInitialState()
    {
        Assert.Equal(GenerationTarget.World, SetupViewModel.MapTarget("World"));
        Assert.Equal(GenerationTarget.InitialWorldState, SetupViewModel.MapTarget("InitialWorldState"));
    }

    [Fact]
    public async Task GenerateKnowledgeAsync_UsesKnowledgeTargetAndDraft()
    {
        var assistant = new FakeGenerationAssistant();
        var setup = new SetupViewModel(new DiffPlexTextDiff(), [], [], assistant, new FakeKnowledgeImporter(), new FakeProjectReviewAssistant());
        var entry = new KnowledgeEntryEditorViewModel
        {
            Kind = KnowledgeKind.Place,
            Title = "Ashen Reach",
            Tags = "region",
            Content = "A frozen frontier.",
        };
        setup.AddKnowledge(new KnowledgeEntryEditorViewModel { Title = "Frost Keep" });

        await setup.GenerateKnowledgeAsync(entry, "brisk", 1, new GenerationSession(), null, CancellationToken.None);

        Assert.NotNull(assistant.LastRequest);
        Assert.Equal(GenerationTarget.Knowledge, assistant.LastRequest!.Target);
        Assert.Equal("Place", assistant.LastRequest.Context.Fields["Kind"]);
        Assert.Equal("Ashen Reach", assistant.LastRequest.Context.Fields["Title"]);
        Assert.Equal("brisk", assistant.LastRequest.Brief);
        Assert.Contains("Frost Keep", assistant.LastRequest.Avoid);
        Assert.DoesNotContain("Ashen Reach", assistant.LastRequest.Avoid);
        Assert.NotNull(assistant.LastRequest.Snapshot);
    }

    [Fact]
    public async Task GenerateKnowledgeAsync_ExcludesUnsavedEntryFromSnapshot()
    {
        var assistant = new FakeGenerationAssistant();
        var setup = new SetupViewModel(new DiffPlexTextDiff(), [], [], assistant, new FakeKnowledgeImporter(), new FakeProjectReviewAssistant());
        var entry = new KnowledgeEntryEditorViewModel { Kind = KnowledgeKind.Note, Title = "Draft" };

        await setup.GenerateKnowledgeAsync(entry, "brief", 1, new GenerationSession(), null, CancellationToken.None);

        Assert.Empty(assistant.LastRequest!.Snapshot!.Knowledge);
    }

    [Fact]
    public async Task ProposeEntryAsync_IncludesCurrentEntryAndFixInstruction()
    {
        var assistant = new FakeGenerationAssistant();
        var setup = new SetupViewModel(new DiffPlexTextDiff(), [], [], assistant, new FakeKnowledgeImporter(), new FakeProjectReviewAssistant());
        setup.AddKnowledge(new KnowledgeEntryEditorViewModel { Kind = KnowledgeKind.Place, Title = "Ashen Reach", Content = "old body" });

        await setup.ProposeEntryAsync("Ashen Reach", "resolve the finding", CancellationToken.None);

        var request = assistant.LastRequest!;
        Assert.Equal(GenerationTarget.Knowledge, request.Target);
        Assert.Equal(1, request.Variants);
        Assert.Equal("old body", request.Context.Fields["Content"]);
        Assert.False(string.IsNullOrWhiteSpace(request.Instruction));
    }

    [Fact]
    public void PreviewFix_ResolvesKnownEditsAndSkipsUnknown()
    {
        var setup = new SetupViewModel(new DiffPlexTextDiff(), [], [], new FakeGenerationAssistant(), new FakeKnowledgeImporter(), new FakeProjectReviewAssistant());
        var entry = new KnowledgeEntryEditorViewModel { Kind = KnowledgeKind.Place, Title = "Ashen Reach", Content = "old" };
        setup.AddKnowledge(entry);

        var fix = new ReviewFix(
        [
            new ReviewEdit(GenerationTarget.World, string.Empty, "Tone", "hopeful"),
            new ReviewEdit(GenerationTarget.Knowledge, "Ashen Reach", "Content", "new"),
            new ReviewEdit(GenerationTarget.Knowledge, "Missing", "Content", "99"),
        ]);

        var changes = setup.PreviewFix(fix);

        var change = Assert.Single(changes);
        Assert.Equal("Ashen Reach · Content", change.Label);
        Assert.Equal("new", change.NewValue);
    }

    [Fact]
    public void ApplyFix_WritesKnowledgeValue()
    {
        var setup = new SetupViewModel(new DiffPlexTextDiff(), [], [], new FakeGenerationAssistant(), new FakeKnowledgeImporter(), new FakeProjectReviewAssistant());
        var entry = new KnowledgeEntryEditorViewModel { Kind = KnowledgeKind.Place, Title = "Ashen Reach", Content = "old" };
        setup.AddKnowledge(entry);

        setup.ApplyFix(new ReviewFix(
        [
            new ReviewEdit(GenerationTarget.Knowledge, "Ashen Reach", "Content", "new"),
        ]), "Fix: test");

        Assert.Equal("new", entry.Content);
    }

    [Fact]
    public void PreviewFix_ResolvesCreateAndDelete()
    {
        var setup = new SetupViewModel(new DiffPlexTextDiff(), [], [], new FakeGenerationAssistant(), new FakeKnowledgeImporter(), new FakeProjectReviewAssistant());
        setup.AddKnowledge(new KnowledgeEntryEditorViewModel { Kind = KnowledgeKind.Place, Title = "Ashen Reach", Content = "old" });

        var fixes = setup.PreviewFix(new ReviewFix(
        [
            new ReviewEdit(GenerationTarget.Knowledge, "Dmitri", "Entry", "new body", ReviewEditOperation.Create, KnowledgeKind.Character),
            new ReviewEdit(GenerationTarget.Knowledge, "Ashen Reach", string.Empty, string.Empty, ReviewEditOperation.Delete),
        ]));

        Assert.Equal(2, fixes.Count);
        Assert.Equal("Create Character: Dmitri", fixes[0].Label);
        Assert.Equal("Delete: Ashen Reach", fixes[1].Label);
    }

    [Fact]
    public void ApplyFix_CreatesAndDeletesEntries()
    {
        var setup = new SetupViewModel(new DiffPlexTextDiff(), [], [], new FakeGenerationAssistant(), new FakeKnowledgeImporter(), new FakeProjectReviewAssistant());
        setup.AddKnowledge(new KnowledgeEntryEditorViewModel { Kind = KnowledgeKind.Place, Title = "Ashen Reach", Content = "old" });

        setup.ApplyFix(new ReviewFix(
        [
            new ReviewEdit(GenerationTarget.Knowledge, "Dmitri", "Entry", "new body", ReviewEditOperation.Create, KnowledgeKind.Character),
            new ReviewEdit(GenerationTarget.Knowledge, "Ashen Reach", string.Empty, string.Empty, ReviewEditOperation.Delete),
        ]), "Fix: test");

        var created = Assert.Single(setup.Knowledge);
        Assert.Equal("Dmitri", created.Title);
        Assert.Equal(KnowledgeKind.Character, created.Kind);
    }

    [Fact]
    public void ApplyFix_AddsAndRemovesTags()
    {
        var setup = new SetupViewModel(new DiffPlexTextDiff(), [], [], new FakeGenerationAssistant(), new FakeKnowledgeImporter(), new FakeProjectReviewAssistant());
        var entry = new KnowledgeEntryEditorViewModel { Kind = KnowledgeKind.Place, Title = "Ashen Reach", Tags = "city, port", Content = "old" };
        setup.AddKnowledge(entry);

        setup.ApplyFix(new ReviewFix(
        [
            new ReviewEdit(GenerationTarget.Knowledge, "Ashen Reach", "Tags", "port", ReviewEditOperation.RemoveTag),
            new ReviewEdit(GenerationTarget.Knowledge, "Ashen Reach", "Tags", "capital", ReviewEditOperation.AddTag),
        ]), "Fix: test");

        Assert.Contains("city", entry.TagList);
        Assert.Contains("capital", entry.TagList);
        Assert.DoesNotContain("port", entry.TagList);
    }

    [Fact]
    public void ApplyFix_UpdatesSeveralEntriesInOneFix()
    {
        var setup = new SetupViewModel(new DiffPlexTextDiff(), [], [], new FakeGenerationAssistant(), new FakeKnowledgeImporter(), new FakeProjectReviewAssistant());
        var reach = new KnowledgeEntryEditorViewModel { Kind = KnowledgeKind.Place, Title = "Ashen Reach", Content = "A keep." };
        var notes = new KnowledgeEntryEditorViewModel { Kind = KnowledgeKind.Note, Title = "Notes", Content = "Refers to Ashen Reach." };
        setup.AddKnowledge(reach);
        setup.AddKnowledge(notes);

        setup.ApplyFix(new ReviewFix(
        [
            new ReviewEdit(GenerationTarget.Knowledge, "Ashen Reach", "Title", "Ashen Keep"),
            new ReviewEdit(GenerationTarget.Knowledge, "Notes", "Content", "Refers to Ashen Keep."),
        ]), "Fix: rename");

        Assert.Equal("Ashen Keep", reach.Title);
        Assert.Equal("Refers to Ashen Keep.", notes.Content);
    }

    [Fact]
    public void PreviewFix_ResolvesReferenceByUniqueSubstring()
    {
        var setup = new SetupViewModel(new DiffPlexTextDiff(), [], [], new FakeGenerationAssistant(), new FakeKnowledgeImporter(), new FakeProjectReviewAssistant());
        setup.AddKnowledge(new KnowledgeEntryEditorViewModel { Kind = KnowledgeKind.Place, Title = "Ashen Reach Keep", Content = "old" });

        var changes = setup.PreviewFix(new ReviewFix(
        [
            new ReviewEdit(GenerationTarget.Knowledge, "Ashen Reach", "Content", "new"),
        ]));

        var change = Assert.Single(changes);
        Assert.Equal("new", change.NewValue);
    }

    [Fact]
    public void FixTargets_IncludesKnowledgeEntries()
    {
        var setup = new SetupViewModel(new DiffPlexTextDiff(), [], [], new FakeGenerationAssistant(), new FakeKnowledgeImporter(), new FakeProjectReviewAssistant());
        setup.AddKnowledge(new KnowledgeEntryEditorViewModel { Kind = KnowledgeKind.Place, Title = "Ashen Reach" });

        var targets = setup.FixTargets();

        Assert.Contains(targets, target => target.Target == GenerationTarget.Knowledge && target.Reference == "Ashen Reach");
    }

    [Fact]
    public void SingleReference_ReturnsOnlyWhenExactlyOne()
    {
        var setup = new SetupViewModel(new DiffPlexTextDiff(), [], [], new FakeGenerationAssistant(), new FakeKnowledgeImporter(), new FakeProjectReviewAssistant());

        Assert.Null(setup.SingleReference(GenerationTarget.Knowledge));

        setup.AddKnowledge(new KnowledgeEntryEditorViewModel { Title = "Ashen Reach" });
        Assert.Equal("Ashen Reach", setup.SingleReference(GenerationTarget.Knowledge));

        setup.AddKnowledge(new KnowledgeEntryEditorViewModel { Title = "Frost Keep" });
        Assert.Null(setup.SingleReference(GenerationTarget.Knowledge));
    }
}
