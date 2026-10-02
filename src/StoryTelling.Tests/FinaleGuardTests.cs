using StoryTelling.Application.Chapters;
using StoryTelling.Domain;

namespace StoryTelling.Tests;

public sealed class FinaleGuardTests
{
    [Fact]
    public void IsUnresolved_WithOpenThread_IsTrue()
    {
        var result = Result("A closed final note.", [new KnowledgeChange { Kind = KnowledgeKind.Thread, Title = "Who?", Status = KnowledgeStatus.Open }]);

        Assert.True(FinaleGuard.IsUnresolved(result, []));
    }

    [Fact]
    public void IsUnresolved_WithOpenThreadInComposedKnowledge_IsTrue()
    {
        var result = Result("A closed final note.", []);
        var composed = new List<KnowledgeEntry> { new() { Kind = KnowledgeKind.Thread, Title = "Who?", Status = KnowledgeStatus.Open } };

        Assert.True(FinaleGuard.IsUnresolved(result, composed));
    }

    [Fact]
    public void IsUnresolved_WithSetsUpNextWorldState_IsTrue()
    {
        var result = new ChapterResult(
            "The story ends.",
            "Logline.",
            new WorldState { Situation = "Unresolved: the mechanism remains. Sets Up Next: she resumes keeping the light, with potential for further interaction." },
            [],
            [],
            [], [], 0);

        Assert.True(FinaleGuard.IsUnresolved(result, []));
    }

    [Theory]
    [InlineData("The transmission is over for now—but silence will not last forever.")]
    [InlineData("To be continued.")]
    [InlineData("It will answer again when it chooses to call.")]
    public void IsUnresolved_WithCliffhangerProse_IsTrue(string text)
    {
        var result = Result(text, []);

        Assert.True(FinaleGuard.IsUnresolved(result, []));
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
            [], [], 0);

        Assert.True(FinaleGuard.IsUnresolved(result, []));
    }

    [Fact]
    public void IsUnresolved_WithResolvedThreadAndClosedEnding_IsFalse()
    {
        var result = Result("Elara finally understands, and the reef is quiet.", [new KnowledgeChange { Kind = KnowledgeKind.Thread, Title = "Who?", Status = KnowledgeStatus.Resolved }]);

        Assert.False(FinaleGuard.IsUnresolved(result, []));
    }

    [Fact]
    public void Close_MarksOpenThreadsResolved()
    {
        var thread = new KnowledgeChange { Kind = KnowledgeKind.Thread, Title = "Who?", Status = KnowledgeStatus.Open };
        var result = Result("The end.", [thread]);

        var closed = FinaleCloser.Close(result, []);

        Assert.Equal(KnowledgeStatus.Resolved, closed.KnowledgeChanges.Single().Status);
    }

    [Fact]
    public void Close_ResolvesComposedOpenThreadsNotMentionedByTheChapter()
    {
        var existing = new KnowledgeEntry { Kind = KnowledgeKind.Thread, Title = "The voice", Status = KnowledgeStatus.Open };
        var result = Result("The end.", []);

        var closed = FinaleCloser.Close(result, [existing]);

        var change = Assert.Single(closed.KnowledgeChanges);
        Assert.Equal(KnowledgeStatus.Resolved, change.Status);
        Assert.Equal(existing.Id, change.EntryId);
    }

    [Fact]
    public void Close_StripsContinuationFromWorldState()
    {
        var result = new ChapterResult(
            "The end.",
            "Logline.",
            new WorldState { Situation = "The reef is quiet. Sets Up Next: she keeps the light, with potential for more." },
            [],
            [],
            [], [], 0);

        var closed = FinaleCloser.Close(result, []);

        Assert.DoesNotContain("Sets Up Next", closed.WorldState!.Situation);
        Assert.Contains("The reef is quiet.", closed.WorldState.Situation);
    }

    private static ChapterResult Result(string text, IReadOnlyList<KnowledgeChange> changes) =>
        new(text, "Logline.", new WorldState { TimeAndPlace = "Here" }, changes, [], [], [], 0);
}




