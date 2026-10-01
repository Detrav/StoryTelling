using StoryTelling.Application.Knowledge;
using StoryTelling.Domain;

namespace StoryTelling.Tests;

public sealed class KnowledgeComposerTests
{
    [Fact]
    public void Compose_WithoutChapters_ReturnsBaseCopy()
    {
        var aria = new KnowledgeEntry { Kind = KnowledgeKind.Character, Title = "Aria", Content = "base" };
        var project = new Project { Knowledge = [aria] };

        var knowledge = KnowledgeComposer.Compose(project, 1);

        var entry = Assert.Single(knowledge);
        Assert.Equal("base", entry.Content);
        Assert.NotSame(aria, entry);
    }

    [Fact]
    public void Compose_AppliesOnlyChaptersBeforeTheTarget()
    {
        var aria = new KnowledgeEntry { Kind = KnowledgeKind.Character, Title = "Aria", Content = "base" };
        var project = new Project
        {
            Knowledge = [aria],
            Chapters =
            [
                new Chapter { Number = 1, KnowledgeChanges = [new KnowledgeChange { Operation = KnowledgeChangeOperation.Update, Title = "Aria", Kind = KnowledgeKind.Character, Content = "wounded" }] },
                new Chapter { Number = 2, KnowledgeChanges = [new KnowledgeChange { Operation = KnowledgeChangeOperation.Update, Title = "Aria", Kind = KnowledgeKind.Character, Content = "dead" }] },
            ],
        };

        var beforeTwo = KnowledgeComposer.Compose(project, 2);
        Assert.Equal("wounded", Assert.Single(beforeTwo).Content);

        var beforeThree = KnowledgeComposer.Compose(project, 3);
        Assert.Equal("dead", Assert.Single(beforeThree).Content);

        Assert.Equal("base", aria.Content);
    }

    [Fact]
    public void Apply_CreateUpdateDelete()
    {
        var knowledge = new List<KnowledgeEntry>
        {
            new() { Kind = KnowledgeKind.Item, Title = "Old Map", Content = "A map." },
        };

        KnowledgeComposer.Apply(knowledge,
        [
            new KnowledgeChange { Operation = KnowledgeChangeOperation.Create, Kind = KnowledgeKind.Character, Title = "Ghost", Content = "A presence." },
            new KnowledgeChange { Operation = KnowledgeChangeOperation.Delete, Title = "Old Map" },
        ]);

        var ghost = Assert.Single(knowledge);
        Assert.Equal("Ghost", ghost.Title);
        Assert.DoesNotContain(knowledge, entry => entry.Title == "Old Map");
    }

    [Fact]
    public void ApplyUpdate_MatchesNormalisedTitle()
    {
        var aria = new KnowledgeEntry { Kind = KnowledgeKind.Character, Title = "Dr. Mira Vale", Content = "old" };
        var knowledge = new List<KnowledgeEntry> { aria };

        KnowledgeComposer.Apply(knowledge, [new KnowledgeChange { Operation = KnowledgeChangeOperation.Update, Title = "[Character] dr. mira vale", Kind = KnowledgeKind.Character, Content = "new" }]);

        var entry = Assert.Single(knowledge);
        Assert.Equal("new", entry.Content);
    }

    [Fact]
    public void ApplyUpdate_ForExistingTitle_BecomesUpdate()
    {
        var aria = new KnowledgeEntry { Kind = KnowledgeKind.Character, Title = "Aria", Content = "old" };
        var knowledge = new List<KnowledgeEntry> { aria };

        KnowledgeComposer.Apply(knowledge, [new KnowledgeChange { Operation = KnowledgeChangeOperation.Create, Kind = KnowledgeKind.Character, Title = "Aria", Content = "new" }]);

        var entry = Assert.Single(knowledge);
        Assert.Equal("new", entry.Content);
    }
}
