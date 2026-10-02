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
    public void Apply_MatchesByIdEvenWhenTheTitleChanged()
    {
        var thread = new KnowledgeEntry { Kind = KnowledgeKind.Thread, Title = "Find the traitor", Content = "Who is it?", Status = KnowledgeStatus.Open };
        var knowledge = new List<KnowledgeEntry> { thread };

        KnowledgeComposer.Apply(knowledge, [new KnowledgeChange { Operation = KnowledgeChangeOperation.Update, EntryId = thread.Id, Title = "The traitor revealed", Kind = KnowledgeKind.Thread, Content = "It was Kael.", Status = KnowledgeStatus.Resolved }]);

        var entry = Assert.Single(knowledge);
        Assert.Equal("The traitor revealed", entry.Title);
        Assert.Equal(KnowledgeStatus.Resolved, entry.Status);
    }

    [Fact]
    public void Compose_CreatedIdIsStableAndLaterUpdateMatchesById()
    {
        var created = new KnowledgeChange
        {
            Operation = KnowledgeChangeOperation.Create,
            EntryId = Guid.NewGuid(),
            Kind = KnowledgeKind.Thread,
            Title = "Who is the traitor?",
            Content = "open",
            Status = KnowledgeStatus.Open,
        };
        var project = new Project
        {
            Chapters =
            [
                new Chapter { Number = 1, KnowledgeChanges = [created] },
                new Chapter { Number = 2, KnowledgeChanges = [new KnowledgeChange { Operation = KnowledgeChangeOperation.Update, EntryId = created.EntryId, Kind = KnowledgeKind.Thread, Title = "Who is the traitor?", Content = "still open", Status = KnowledgeStatus.Open }] },
            ],
        };

        var first = KnowledgeComposer.Compose(project, 2);
        var second = KnowledgeComposer.Compose(project, 2);
        Assert.Equal(first[0].Id, second[0].Id);

        var beforeThree = KnowledgeComposer.Compose(project, 3);
        var entry = Assert.Single(beforeThree);
        Assert.Equal(created.EntryId, entry.Id);
        Assert.Equal("still open", entry.Content);
    }

    [Fact]
    public void Apply_CreateThreadCarriesStatus()
    {
        var knowledge = new List<KnowledgeEntry>();

        KnowledgeComposer.Apply(knowledge, [new KnowledgeChange { Operation = KnowledgeChangeOperation.Create, Kind = KnowledgeKind.Thread, Title = "Will Elena survive?", Content = "Open.", Status = KnowledgeStatus.Open }]);

        var entry = Assert.Single(knowledge);
        Assert.Equal(KnowledgeKind.Thread, entry.Kind);
        Assert.Equal(KnowledgeStatus.Open, entry.Status);
    }

    [Fact]
    public void ApplyUpdate_MergesTagsInsteadOfReplacingThem()
    {
        var elara = new KnowledgeEntry
        {
            Kind = KnowledgeKind.Character,
            Title = "Elara Voss",
            Tags = ["Lighthouse keeper", "Former signal technician"],
            Content = "base",
        };
        var knowledge = new List<KnowledgeEntry> { elara };

        KnowledgeComposer.Apply(knowledge, [new KnowledgeChange
        {
            Operation = KnowledgeChangeOperation.Update,
            Kind = KnowledgeKind.Character,
            Title = "Elara Voss",
            Tags = ["Main Character", "lighthouse keeper"],
            Content = "updated",
        }]);

        var entry = Assert.Single(knowledge);
        Assert.Equal(["Lighthouse keeper", "Former signal technician", "Main Character"], entry.Tags);
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
