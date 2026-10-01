using StoryTelling.Application.Generation;

namespace StoryTelling.Tests;

public sealed class GenerationTargetsTests
{
    [Fact]
    public void BuildSchema_TraitsAndTagsAreStringArrays()
    {
        var character = GenerationTargets.BuildSchema(GenerationTarget.Character, 1).ToJsonString();
        var knowledge = GenerationTargets.BuildSchema(GenerationTarget.Knowledge, 1).ToJsonString();

        Assert.Contains("\"traits\":{\"type\":\"array\"", character);
        Assert.Contains("\"tags\":{\"type\":\"array\"", knowledge);
    }
}
