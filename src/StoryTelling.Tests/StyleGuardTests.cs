using StoryTelling.Application.Chapters;

namespace StoryTelling.Tests;

public sealed class StyleGuardTests
{
    [Fact]
    public void FindViolations_DetectsChapterReferencesAndMetaPhrases()
    {
        var text = "As we saw back in Chapter One, the rain never stopped. In this chapter the story so far is clear.";

        var violations = StyleGuard.FindViolations(text);

        Assert.Contains("Chapter One", violations);
        Assert.Contains("this chapter", violations);
        Assert.Contains("the story so far", violations);
    }

    [Fact]
    public void FindViolations_CleanProse_ReturnsEmpty()
    {
        const string text = "The rain tasted like copper. Elias stepped into the shattered corridor and kept walking.";

        Assert.Empty(StyleGuard.FindViolations(text));
    }

    [Fact]
    public void FindViolations_IgnoresOrdinaryWords()
    {
        const string text = "A novel chapter of his life closed as the precinct fell silent.";

        Assert.Empty(StyleGuard.FindViolations(text));
    }

    [Fact]
    public void RemoveMeta_StripsChapterReferences()
    {
        var cleaned = StyleGuard.RemoveMeta("Confirmed in Chapter 6 that the map changes.");

        Assert.DoesNotContain("Chapter 6", cleaned);
    }

    [Fact]
    public void DriftsFromTense_DetectsPastNarrationWhenPresentIsDeclared()
    {
        const string past = "The rain was cold. He had a key. She was there. They were gone. I was alone. It had ended. He did not move.";
        const string present = "The rain is cold. He has a key. She is there. They are gone. I am alone. It is over. He does not move.";

        Assert.True(StyleGuard.DriftsFromTense(past, "Present tense for immediacy"));
        Assert.False(StyleGuard.DriftsFromTense(present, "Present tense for immediacy"));
    }
}
