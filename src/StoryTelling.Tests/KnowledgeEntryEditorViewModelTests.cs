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

    [Fact]
    public void ToFields_IncludesKindAndValues()
    {
        var editor = new KnowledgeEntryEditorViewModel
        {
            Kind = KnowledgeKind.Item,
            Title = "Ember Crown",
            Tags = "relic",
            Content = "A relic.",
        };

        var fields = editor.ToFields();

        Assert.Equal("Item", fields["Kind"]);
        Assert.Equal("Ember Crown", fields["Title"]);
        Assert.Equal("relic", fields["Tags"]);
        Assert.Equal("A relic.", fields["Content"]);
    }

    [Fact]
    public void ApplyFields_ParsesKindAndSetsFields()
    {
        var editor = new KnowledgeEntryEditorViewModel
        {
            Kind = KnowledgeKind.Note,
            Title = "Old",
            Tags = "old",
            Content = "old content",
        };

        editor.ApplyFields(new Dictionary<string, string>
        {
            ["Kind"] = "place",
            ["Title"] = "  Ashen Reach  ",
            ["Tags"] = "region, cold",
            ["Content"] = "A frozen frontier.",
        });

        Assert.Equal(KnowledgeKind.Place, editor.Kind);
        Assert.Equal("Ashen Reach", editor.Title);
        Assert.Equal("region, cold", editor.Tags);
        Assert.Equal("A frozen frontier.", editor.Content);
    }

    [Fact]
    public void ApplyFields_UnknownKind_KeepsCurrent()
    {
        var editor = new KnowledgeEntryEditorViewModel { Kind = KnowledgeKind.Faction };

        editor.ApplyFields(new Dictionary<string, string> { ["Kind"] = "Creature" });

        Assert.Equal(KnowledgeKind.Faction, editor.Kind);
    }
}
