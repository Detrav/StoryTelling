using StoryTelling.Application.Chapters;
using StoryTelling.Domain;

namespace StoryTelling.Tests;

public sealed class ChapterTextCleanerTests
{
    [Fact]
    public void Strip_RemovesExactTitleLine()
    {
        var text = "Embers\nThe smoke rose over the ridge.";

        var result = ChapterTextCleaner.StripLeadingTitle(text, new Chapter { Title = "Embers" });

        Assert.Equal("The smoke rose over the ridge.", result);
    }

    [Fact]
    public void Strip_RemovesMarkdownTitleHeading()
    {
        var text = "# Embers\nThe smoke rose.";

        var result = ChapterTextCleaner.StripLeadingTitle(text, new Chapter { Title = "Embers" });

        Assert.Equal("The smoke rose.", result);
    }

    [Fact]
    public void Strip_RemovesChapterHeadingWithTitle()
    {
        var text = "Chapter 1: Embers\nThe smoke rose.";

        var result = ChapterTextCleaner.StripLeadingTitle(text, new Chapter { Title = "Embers" });

        Assert.Equal("The smoke rose.", result);
    }

    [Fact]
    public void Strip_RemovesBareChapterHeading()
    {
        var text = "Chapter 2\nThe smoke rose.";

        var result = ChapterTextCleaner.StripLeadingTitle(text, new Chapter { Title = "Embers" });

        Assert.Equal("The smoke rose.", result);
    }

    [Fact]
    public void Strip_KeepsUnrelatedFirstLine()
    {
        var text = "The morning was cold and grey.\nMore prose.";

        var result = ChapterTextCleaner.StripLeadingTitle(text, new Chapter { Title = "Embers" });

        Assert.Equal(text, result);
    }

    [Fact]
    public void Strip_KeepsProseThatMentionsTheTitle()
    {
        var text = "The embers of the old fire still glowed.\nMore prose.";

        var result = ChapterTextCleaner.StripLeadingTitle(text, new Chapter { Title = "Embers" });

        Assert.Equal(text, result);
    }
}
