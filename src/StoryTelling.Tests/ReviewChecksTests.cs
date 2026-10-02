using StoryTelling.Application.Review;

namespace StoryTelling.Tests;

public sealed class ReviewChecksTests
{
    [Fact]
    public void ProjectScope_ExcludesContinuity()
    {
        var projectChecks = ReviewChecks.ForScope(ReviewScope.Project);

        Assert.Contains(projectChecks, check => check.Id == "numbers");
        Assert.Contains(projectChecks, check => check.Id == "facts");
        Assert.DoesNotContain(projectChecks, check => check.Scope == ReviewScope.Chapter);
    }

    [Fact]
    public void All_IdsAreUnique()
    {
        var ids = ReviewChecks.All.Select(check => check.Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Find_MatchesByIdCaseInsensitive()
    {
        Assert.Equal("numbers", ReviewChecks.Find("NUMBERS")!.Id);
        Assert.Null(ReviewChecks.Find("nope"));
    }
}
