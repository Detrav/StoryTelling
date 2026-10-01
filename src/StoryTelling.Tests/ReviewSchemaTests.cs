using StoryTelling.Application.Review;

namespace StoryTelling.Tests;

public sealed class ReviewSchemaTests
{
    [Fact]
    public void Build_IncludesFixEditsAndFieldVocabulary()
    {
        var schema = ReviewSchema.Build().ToJsonString();

        Assert.Contains("\"fix\"", schema);
        Assert.Contains("\"edits\"", schema);
        Assert.Contains("\"reference\"", schema);
        Assert.Contains("WorldState", schema);
        Assert.Contains("TimeAndPlace", schema);
        Assert.Contains("Knowledge", schema);
    }
}
