using StoryTelling.Application.Review;
using StoryTelling.Domain;

namespace StoryTelling.Tests;

public sealed class ContinuityReviewerTests
{
    [Fact]
    public async Task ReviewAsync_ParsesFindings()
    {
        const string json = """{"findings":[{"severity":"Error","title":"Impossible parentage","detail":"Elena is 26 with a 26-year-old son.","reference":"Elena Volkov"}]}""";
        var reviewer = new ContinuityReviewer(new FakeLlmClient(json), new FakeSettingsService());
        var project = new Project
        {
            Chapters = [new Chapter { Number = 1, Direction = "Elena raises her son." }],
        };

        var findings = await reviewer.ReviewAsync(project, project.Chapters[0]);

        var finding = Assert.Single(findings);
        Assert.Equal(ReviewSeverity.Error, finding.Severity);
        Assert.Equal("Impossible parentage", finding.Title);
        Assert.Equal("Elena Volkov", finding.Reference);
    }

    [Fact]
    public async Task ReviewAsync_EmptyFindings_ReturnsEmpty()
    {
        var reviewer = new ContinuityReviewer(new FakeLlmClient("""{"findings":[]}"""), new FakeSettingsService());

        var findings = await reviewer.ReviewAsync(new Project { Chapters = [new Chapter { Number = 1 }] }, new Chapter { Number = 1 });

        Assert.Empty(findings);
    }
}
