using StoryTelling.Application.Review;

namespace StoryTelling.Tests;

public sealed class ReviewSchemaTests
{
    [Fact]
    public void Build_IncludesFixEditsAndKnowledgeVocabulary()
    {
        var schema = ReviewSchema.Build().ToJsonString();

        Assert.Contains("\"fix\"", schema);
        Assert.Contains("\"edits\"", schema);
        Assert.Contains("\"reference\"", schema);
        Assert.Contains("Knowledge", schema);
        Assert.Contains("Content", schema);
        Assert.DoesNotContain("TimeAndPlace", schema);
    }
}
