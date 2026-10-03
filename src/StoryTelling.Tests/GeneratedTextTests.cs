using StoryTelling.Application.Generation;

namespace StoryTelling.Tests;

public sealed class GeneratedTextTests
{
    [Fact]
    public void IsPlausible_AllowsLitRpgBrackets()
    {
        Assert.True(GeneratedText.IsPlausible("[AWAITING BOOTSTRAP] The hall was cold and the System blinked."));
    }

    [Fact]
    public void IsPlausible_RejectsJsonObjects()
    {
        Assert.False(GeneratedText.IsPlausible("{\"situation\":\"x\"}"));
    }
}
