using StoryTelling.Application.Generation;
using StoryTelling.Application.Review;
using StoryTelling.Domain;
using StoryTelling.Infrastructure.Diff;
using StoryTelling.ViewModels;

namespace StoryTelling.Tests;

public sealed class SetupViewModelTests
{
    [Fact]
    public void AddCharacter_And_RemoveCharacter_UpdateCollection()
    {
        var setup = new SetupViewModel(new DiffPlexTextDiff(), [], [], new FakeGenerationAssistant(), new FakeKnowledgeImporter(), new FakeProjectReviewAssistant());
        var character = new CharacterEditorViewModel { Name = "Aria", Traits = "brave, quick" };

        setup.AddCharacter(character);

        Assert.Same(character, setup.SelectedCharacter);
        Assert.Equal(new[] { "brave", "quick" }, character.TraitList);

        setup.RemoveCharacter(character);

        Assert.Empty(setup.Characters);
        Assert.Null(setup.SelectedCharacter);
    }

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
    public void ApplyGenerated_SetsPlotFields()
    {
        var setup = new SetupViewModel(new DiffPlexTextDiff(), [], [], new FakeGenerationAssistant(), new FakeKnowledgeImporter(), new FakeProjectReviewAssistant());

        setup.ApplyGenerated(new Dictionary<string, string>
        {
            ["Genre"] = "dark fantasy",
            ["Tone"] = "grim",
            ["Premise"] = "A scout hunts the truth.",
            ["Direction"] = "She uncovers a conspiracy.",
        });

        Assert.Equal("dark fantasy", setup.Genre);
        Assert.Equal("grim", setup.Tone);
        Assert.Equal("A scout hunts the truth.", setup.Premise);
        Assert.Equal("She uncovers a conspiracy.", setup.Direction);
    }

    [Fact]
    public void ApplyGenerated_SetsWorldStateFields()
    {
        var setup = new SetupViewModel(new DiffPlexTextDiff(), [], [], new FakeGenerationAssistant(), new FakeKnowledgeImporter(), new FakeProjectReviewAssistant());

        setup.ApplyGenerated(new Dictionary<string, string>
        {
            ["TimeAndPlace"] = "Dusk above the keep",
            ["Description"] = "Aria crouches in the ruins.",
        });

        Assert.Equal("Dusk above the keep", setup.WorldStateTimeAndPlace);
        Assert.Equal("Aria crouches in the ruins.", setup.WorldStateDescription);
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
    public void MapTarget_MapsFrame()
    {
        Assert.Equal(GenerationTarget.Frame, SetupViewModel.MapTarget("Frame"));
    }

    [Fact]
    public async Task GenerateCharacterAsync_UsesCharacterTargetAndDraft()
    {
        var assistant = new FakeGenerationAssistant();
        var setup = new SetupViewModel(new DiffPlexTextDiff(), [], [], assistant, new FakeKnowledgeImporter(), new FakeProjectReviewAssistant());
        var character = new CharacterEditorViewModel { Name = "Aria", Role = "protagonist" };

        await setup.GenerateCharacterAsync(character, "brisk", 1, new GenerationSession(), null, CancellationToken.None);

        Assert.NotNull(assistant.LastRequest);
        Assert.Equal(GenerationTarget.Character, assistant.LastRequest!.Target);
        Assert.Equal("Aria", assistant.LastRequest.Context.Fields["Name"]);
        Assert.Equal("brisk", assistant.LastRequest.Brief);
        Assert.Equal(1, assistant.LastRequest.Variants);
        Assert.NotNull(assistant.LastRequest.Snapshot);
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

        await setup.GenerateKnowledgeAsync(entry, "brisk", 1, new GenerationSession(), null, CancellationToken.None);

        Assert.NotNull(assistant.LastRequest);
        Assert.Equal(GenerationTarget.Knowledge, assistant.LastRequest!.Target);
        Assert.Equal("Place", assistant.LastRequest.Context.Fields["Kind"]);
        Assert.Equal("Ashen Reach", assistant.LastRequest.Context.Fields["Title"]);
        Assert.Equal("brisk", assistant.LastRequest.Brief);
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
    public async Task GenerateCharacterAsync_ExcludesCurrentAndIncludesOthers()
    {
        var assistant = new FakeGenerationAssistant();
        var setup = new SetupViewModel(new DiffPlexTextDiff(), [], [], assistant, new FakeKnowledgeImporter(), new FakeProjectReviewAssistant());
        var aria = new CharacterEditorViewModel { Name = "Aria", Role = "protagonist" };
        var bran = new CharacterEditorViewModel { Name = "Bran", Role = "smith" };
        setup.AddCharacter(aria);
        setup.AddCharacter(bran);

        await setup.GenerateCharacterAsync(aria, "brief", 2, new GenerationSession(), null, CancellationToken.None);

        var cast = assistant.LastRequest!.Context.Cast;
        Assert.Contains(cast, entry => entry.Contains("Bran"));
        Assert.DoesNotContain(cast, entry => entry.Contains("Aria"));
    }

    [Fact]
    public void PreviewFix_ResolvesKnownEditsAndSkipsUnknown()
    {
        var setup = new SetupViewModel(new DiffPlexTextDiff(), [], [], new FakeGenerationAssistant(), new FakeKnowledgeImporter(), new FakeProjectReviewAssistant());
        var aria = new CharacterEditorViewModel { Name = "Aria", Role = "protagonist" };
        setup.AddCharacter(aria);
        var entry = new KnowledgeEntryEditorViewModel { Kind = KnowledgeKind.Place, Title = "Ashen Reach", Content = "old" };
        setup.AddKnowledge(entry);

        var fix = new ReviewFix(
        [
            new ReviewEdit(GenerationTarget.Frame, string.Empty, "Tone", "hopeful"),
            new ReviewEdit(GenerationTarget.Character, "Aria", "Age", "20"),
            new ReviewEdit(GenerationTarget.Knowledge, "Ashen Reach", "Content", "new"),
            new ReviewEdit(GenerationTarget.Character, "Missing", "Age", "99"),
        ]);

        var changes = setup.PreviewFix(fix);

        Assert.Equal(3, changes.Count);
        Assert.Contains(changes, change => change.Label == "Tone");
        Assert.Contains(changes, change => change.Label == "Aria · Age" && change.NewValue == "20");
        Assert.Contains(changes, change => change.Label == "Ashen Reach · Content");
    }

    [Fact]
    public void ApplyFix_WritesValuesForSingletonAndCharacter()
    {
        var setup = new SetupViewModel(new DiffPlexTextDiff(), [], [], new FakeGenerationAssistant(), new FakeKnowledgeImporter(), new FakeProjectReviewAssistant());
        var aria = new CharacterEditorViewModel { Name = "Aria" };
        setup.AddCharacter(aria);

        setup.ApplyFix(new ReviewFix(
        [
            new ReviewEdit(GenerationTarget.Frame, string.Empty, "Tone", "hopeful"),
            new ReviewEdit(GenerationTarget.Character, "Aria", "Age", "20"),
        ]), "Fix: test");

        Assert.Equal("hopeful", setup.Tone);
        Assert.Equal("20", aria.Age);
    }

    [Fact]
    public void FixTargets_IncludesSingletonsAndNamedEntities()
    {
        var setup = new SetupViewModel(new DiffPlexTextDiff(), [], [], new FakeGenerationAssistant(), new FakeKnowledgeImporter(), new FakeProjectReviewAssistant());
        setup.AddCharacter(new CharacterEditorViewModel { Name = "Aria" });
        setup.AddKnowledge(new KnowledgeEntryEditorViewModel { Kind = KnowledgeKind.Place, Title = "Ashen Reach" });

        var targets = setup.FixTargets();

        Assert.Contains(targets, target => target.Target == GenerationTarget.World);
        Assert.Contains(targets, target => target.Target == GenerationTarget.Character && target.Reference == "Aria");
        Assert.Contains(targets, target => target.Target == GenerationTarget.Knowledge && target.Reference == "Ashen Reach");
    }

    [Fact]
    public void SingleReference_ReturnsOnlyWhenExactlyOne()
    {
        var setup = new SetupViewModel(new DiffPlexTextDiff(), [], [], new FakeGenerationAssistant(), new FakeKnowledgeImporter(), new FakeProjectReviewAssistant());

        Assert.Null(setup.SingleReference(GenerationTarget.Character));

        setup.AddCharacter(new CharacterEditorViewModel { Name = "Aria" });
        Assert.Equal("Aria", setup.SingleReference(GenerationTarget.Character));

        setup.AddCharacter(new CharacterEditorViewModel { Name = "Bran" });
        Assert.Null(setup.SingleReference(GenerationTarget.Character));
    }
}
