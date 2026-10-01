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
