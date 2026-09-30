using StoryTelling.Domain;
using StoryTelling.ViewModels;

namespace StoryTelling.Tests;

public sealed class KnowledgeEntryEditorViewModelTests
{
    [Fact]
    public void ToEntry_MapsFieldsAndParsesTags()
    {
        var editor = new KnowledgeEntryEditorViewModel
        {
            Kind = KnowledgeKind.Item,
            Title = "  The Ember Crown  ",
            Tags = "old, powerful , ",
            Content = "A relic of the old kingdom.",
        };

        var entry = editor.ToEntry();

        Assert.Equal(KnowledgeKind.Item, entry.Kind);
        Assert.Equal("The Ember Crown", entry.Title);
        Assert.Equal(new[] { "old", "powerful" }, entry.Tags);
        Assert.Equal("A relic of the old kingdom.", entry.Content);
    }

    [Fact]
    public void FromImport_UsesFileNameAsTitle()
    {
        var editor = KnowledgeEntryEditorViewModel.FromImport("bestiary.md", "Wyverns nest in cliffs.");

        Assert.Equal(KnowledgeKind.Note, editor.Kind);
        Assert.Equal("bestiary", editor.Title);
        Assert.Equal("Wyverns nest in cliffs.", editor.Content);
    }

    [Fact]
    public void CloneAndCopyFrom_PreserveFields()
    {
        var original = new KnowledgeEntryEditorViewModel
        {
            Kind = KnowledgeKind.Event,
            Title = "The fall of the keep",
            Tags = "history",
            Content = "It fell at dusk.",
        };

        var clone = original.Clone();
        var target = new KnowledgeEntryEditorViewModel();
        target.CopyFrom(clone);

        Assert.Equal(original.Id, clone.Id);
        Assert.Equal(KnowledgeKind.Event, target.Kind);
        Assert.Equal("The fall of the keep", target.Title);
        Assert.Equal("history", target.Tags);
        Assert.Equal("It fell at dusk.", target.Content);
    }
}
