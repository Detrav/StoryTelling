using StoryTelling.Application.Chapters;
using StoryTelling.Domain;

namespace StoryTelling.Tests;

public sealed class FinaleGuardTests
{
    [Fact]
    public void IsUnresolved_WithOpenThread_IsTrue()
    {
        var result = Result("A closed final note.", [new KnowledgeChange { Kind = KnowledgeKind.Thread, Title = "Who?", Status = KnowledgeStatus.Open }]);

        Assert.True(FinaleGuard.IsUnresolved(result));
    }

    [Theory]
    [InlineData("The transmission is over for now—but silence will not last forever.")]
    [InlineData("To be continued.")]
    [InlineData("It will answer again when it chooses to call.")]
    public void IsUnresolved_WithCliffhangerProse_IsTrue(string text)
    {
        var result = Result(text, []);

        Assert.True(FinaleGuard.IsUnresolved(result));
    }

    [Fact]
    public void IsUnresolved_WithCliffhangerWorldState_IsTrue()
    {
        var result = new ChapterResult(
            "The story ends.",
            "Logline.",
            new WorldState { Situation = "This sets up future interactions with the living signal." },
            [],
            [],
            0);

        Assert.True(FinaleGuard.IsUnresolved(result));
    }

    [Fact]
    public void IsUnresolved_WithResolvedThreadAndClosedEnding_IsFalse()
    {
        var result = Result("Elara finally understands, and the reef is quiet.", [new KnowledgeChange { Kind = KnowledgeKind.Thread, Title = "Who?", Status = KnowledgeStatus.Resolved }]);

        Assert.False(FinaleGuard.IsUnresolved(result));
    }

    private static ChapterResult Result(string text, IReadOnlyList<KnowledgeChange> changes) =>
        new(text, "Logline.", new WorldState { TimeAndPlace = "Here" }, changes, [], 0);
}
