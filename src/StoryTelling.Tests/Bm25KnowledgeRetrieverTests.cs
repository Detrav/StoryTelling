using StoryTelling.Application.Retrieval;
using StoryTelling.Domain;

namespace StoryTelling.Tests;

public sealed class Bm25KnowledgeRetrieverTests
{
    [Fact]
    public void Search_RanksRelevantEntryFirst()
    {
        var retriever = new Bm25KnowledgeRetriever(
        [
            Entry("Ashen Reach", "A frozen frontier of ash and ruined keeps."),
            Entry("The Ember Crown", "A relic of dead magic hidden beneath the keep."),
            Entry("Bestiary", "Wyverns nest in the cliffs above the sea."),
        ]);

        var results = retriever.Search("ember crown relic", kind: null, topK: 3);

        Assert.NotEmpty(results);
        Assert.Equal("The Ember Crown", results[0].Title);
    }

    [Fact]
    public void Search_FiltersByKind()
    {
        var retriever = new Bm25KnowledgeRetriever(
        [
            Entry("Relic", "The crown is a relic.", KnowledgeKind.Item),
            Entry("Note about relic", "The relic is mentioned in passing.", KnowledgeKind.Note),
        ]);

        var results = retriever.Search("relic", KnowledgeKind.Item, topK: 5);

        Assert.NotEmpty(results);
        Assert.All(results, fragment => Assert.Equal(KnowledgeKind.Item, fragment.Kind));
    }

    [Fact]
    public void Search_RespectsTopK()
    {
        var retriever = new Bm25KnowledgeRetriever(
        [
            Entry("A", "ash ash ash"),
            Entry("B", "ash"),
            Entry("C", "ash"),
        ]);

        var results = retriever.Search("ash", kind: null, topK: 2);

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void Search_NoMatch_ReturnsEmpty()
    {
        var retriever = new Bm25KnowledgeRetriever([Entry("A", "ash and ember")]);

        Assert.Empty(retriever.Search("dragons", kind: null, topK: 5));
    }

    private static KnowledgeEntry Entry(string title, string content, KnowledgeKind kind = KnowledgeKind.Note, params string[] tags) => new()
    {
        Title = title,
        Content = content,
        Kind = kind,
        Tags = [.. tags],
    };
}
