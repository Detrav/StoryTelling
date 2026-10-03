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
        Assert.Contains("\"reconciliation\"", schema);
        Assert.Contains("Knowledge", schema);
        Assert.Contains("Content", schema);
        Assert.DoesNotContain("TimeAndPlace", schema);
    }

    [Fact]
    public void Build_WithoutReconciliation_OmitsTheField()
    {
        var with = ReviewSchema.Build(includeReconciliation: false).ToJsonString();

        Assert.DoesNotContain("\"reconciliation\"", with);
        Assert.Contains("\"findings\"", with);
    }

    [Fact]
    public void Build_DoesNotRequireFixOnFindings()
    {
        var finding = ReviewSchema.Build()["properties"]!["findings"]!["items"]!["required"]!.AsArray();
        var required = finding.Select(node => node!.GetValue<string>()).ToList();

        Assert.DoesNotContain("fix", required);
        Assert.Contains("detail", required);
    }
}
