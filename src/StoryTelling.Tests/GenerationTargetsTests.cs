using StoryTelling.Application.Generation;

namespace StoryTelling.Tests;

public sealed class GenerationTargetsTests
{
    [Fact]
    public void BuildSchema_WorldAndKnowledgeFields()
    {
        var world = GenerationTargets.BuildSchema(GenerationTarget.World, 1).ToJsonString();
        var knowledge = GenerationTargets.BuildSchema(GenerationTarget.Knowledge, 1).ToJsonString();

        Assert.Contains("\"title\":{\"type\":\"string\"", world);
        Assert.Contains("\"tags\":{\"type\":\"array\"", knowledge);
    }

    [Fact]
    public void BuildSchema_ChapterSettingsHasTitleAndDirection()
    {
        var schema = GenerationTargets.BuildSchema(GenerationTarget.ChapterSettings, 1).ToJsonString();

        Assert.Contains("\"title\"", schema);
        Assert.Contains("\"direction\"", schema);
    }
}
