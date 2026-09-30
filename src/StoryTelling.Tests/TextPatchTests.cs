using StoryTelling.Infrastructure.Diff;

namespace StoryTelling.Tests;

public sealed class TextPatchTests
{
    private static readonly DiffPlexTextDiff Diff = new();

    [Theory]
    [InlineData("a\nb\nc", "a\nB\nc")]
    [InlineData("a\nb\nc", "a\nc")]
    [InlineData("a\nb\nc", "a\nb\nc\nd")]
    [InlineData("a\nb\nc\nd", "a\nX\nc\nY")]
    [InlineData("", "hello")]
    [InlineData("hello", "")]
    [InlineData("x\ny", "x\ny")]
    [InlineData("a\r\nb", "a\nb")]
    [InlineData("a\nb", "a\nb\n")]
    [InlineData("  ", " ")]
    [InlineData("привет мир", "привет мир!")]
    [InlineData("a\nb\nc", "c\nb\na")]
    [InlineData("\n\n", "")]
    [InlineData("", "\n")]
    public void Patch_AppliesAndReverts(string oldText, string newText)
    {
        var patch = Diff.CreatePatch(oldText, newText);

        Assert.Equal(newText, patch.Apply(oldText));
        Assert.Equal(oldText, patch.Revert(newText));
    }

    [Fact]
    public void Patch_IdenticalText_IsEmpty()
    {
        var patch = Diff.CreatePatch("same\ntext", "same\ntext");

        Assert.True(patch.IsEmpty);
    }
}
