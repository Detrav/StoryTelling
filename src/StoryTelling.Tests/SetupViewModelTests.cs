using StoryTelling.Application.Generation;
using StoryTelling.Domain;
using StoryTelling.Infrastructure.Diff;
using StoryTelling.ViewModels;

namespace StoryTelling.Tests;

public sealed class SetupViewModelTests
{
    [Fact]
    public void AddCharacter_And_RemoveCharacter_UpdateCollection()
    {
        var setup = new SetupViewModel(new DiffPlexTextDiff(), [], [], new FakeGenerationAssistant());
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
        var setup = new SetupViewModel(new DiffPlexTextDiff(), [], [], new FakeGenerationAssistant());

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
        var setup = new SetupViewModel(new DiffPlexTextDiff(), [], [], new FakeGenerationAssistant());

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
        var setup = new SetupViewModel(new DiffPlexTextDiff(), [], [], new FakeGenerationAssistant());

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
        var setup = new SetupViewModel(new DiffPlexTextDiff(), [], [], new FakeGenerationAssistant());
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
        var setup = new SetupViewModel(new DiffPlexTextDiff(), [], [], assistant);
        var character = new CharacterEditorViewModel { Name = "Aria", Role = "protagonist" };

        await setup.GenerateCharacterAsync(character, "brisk", 1, CancellationToken.None);

        Assert.NotNull(assistant.LastRequest);
        Assert.Equal(GenerationTarget.Character, assistant.LastRequest!.Target);
        Assert.Equal("Aria", assistant.LastRequest.Context.Fields["Name"]);
        Assert.Equal("brisk", assistant.LastRequest.Brief);
        Assert.Equal(1, assistant.LastRequest.Variants);
    }

    [Fact]
    public async Task GenerateCharacterAsync_ExcludesCurrentAndIncludesOthers()
    {
        var assistant = new FakeGenerationAssistant();
        var setup = new SetupViewModel(new DiffPlexTextDiff(), [], [], assistant);
        var aria = new CharacterEditorViewModel { Name = "Aria", Role = "protagonist" };
        var bran = new CharacterEditorViewModel { Name = "Bran", Role = "smith" };
        setup.AddCharacter(aria);
        setup.AddCharacter(bran);

        await setup.GenerateCharacterAsync(aria, "brief", 2, CancellationToken.None);

        var cast = assistant.LastRequest!.Context.Cast;
        Assert.Contains(cast, entry => entry.Contains("Bran"));
        Assert.DoesNotContain(cast, entry => entry.Contains("Aria"));
    }
}
