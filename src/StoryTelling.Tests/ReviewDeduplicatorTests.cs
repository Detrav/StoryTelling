using StoryTelling.Application.Generation;
using StoryTelling.Application.Review;
using StoryTelling.Domain;

namespace StoryTelling.Tests;

public sealed class ReviewDeduplicatorTests
{
    private static Project Project(params (KnowledgeKind Kind, string Title)[] entries) => new()
    {
        Knowledge = [.. entries.Select(entry => new KnowledgeEntry { Kind = entry.Kind, Title = entry.Title, Content = "x" })],
    };

    private static ReviewFinding Finding(string title, string detail, string? reference = null, ReviewFix? fix = null, ReviewSeverity severity = ReviewSeverity.Warning) =>
        new(severity, ReviewArea.Knowledge, title, detail, null, fix, reference);

    [Fact]
    public void MergesSameReferenceWithSimilarTitles()
    {
        var project = Project((KnowledgeKind.Event, "The Neon Alley Raid (2019)"));
        var findings = new[]
        {
            Finding("Dmitri age progression inconsistency", "Dmitri is 20 in 2019 and 26 in 2024.", "The Neon Alley Raid (2019)"),
            Finding("Dmitri age inconsistency across events", "Dmitri is 20 in 2019 and 26 in 2024, impossible.", "The Neon Alley Raid (2019)"),
        };

        var result = ReviewDeduplicator.Deduplicate(findings, project);

        Assert.Single(result);
    }

    [Fact]
    public void KeepsSameReferenceDifferentIssuesApart()
    {
        var project = Project((KnowledgeKind.Character, "Elena Volkov"));
        var findings = new[]
        {
            Finding("Elena age vs son birth year", "Elena is 26 and her son is 24; impossible parent age.", "Elena Volkov"),
            Finding("Elena rank timeline drift", "Junior detective in 2019 but lieutenant after two years.", "Elena Volkov"),
        };

        var result = ReviewDeduplicator.Deduplicate(findings, project);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void MergesNormalizedExactTitles()
    {
        var project = Project();
        var findings = new[]
        {
            Finding("Real-World Place Names", "Pervomaysky is in Moscow.", null),
            Finding("real world place names", "Pervomaysky is a Moscow district.", null),
        };

        var result = ReviewDeduplicator.Deduplicate(findings, project);

        Assert.Single(result);
    }

    [Fact]
    public void MergeTakesHighestSeverityLongestDetailAndCombinesFixes()
    {
        var project = Project((KnowledgeKind.Character, "Morozov"));
        var fixA = new ReviewFix([new ReviewEdit(GenerationTarget.Knowledge, "Morozov", "Content", "a")]);
        var fixB = new ReviewFix([new ReviewEdit(GenerationTarget.Knowledge, "Morozov", "Tags", "b")]);
        var findings = new[]
        {
            Finding("Morozov age mismatch", "short", "Morozov", fixA, ReviewSeverity.Info),
            Finding("Morozov age mismatch tag", "a much longer detail explaining the contradiction", "Morozov", fixB, ReviewSeverity.Error),
        };

        var result = ReviewDeduplicator.Deduplicate(findings, project);

        var merged = Assert.Single(result);
        Assert.Equal(ReviewSeverity.Error, merged.Severity);
        Assert.Equal("a much longer detail explaining the contradiction", merged.Detail);
        Assert.Equal(2, merged.Fix!.Edits.Count);
    }

    [Fact]
    public void CanonicalizesReferenceToEntryTitle()
    {
        var project = Project((KnowledgeKind.Character, "Captain Viktor Morozov"));
        var findings = new[]
        {
            Finding("Age mismatch", "tag says 50, content says 52", "Captain Viktor Morozov"),
            Finding("Age mismatch tag", "tag says 50, content says 52 years old", "captain viktor morozov"),
        };

        var result = ReviewDeduplicator.Deduplicate(findings, project);

        Assert.Single(result);
        Assert.Equal("Captain Viktor Morozov", result[0].Reference);
    }
}
