using StoryTelling.ViewModels;

namespace StoryTelling.Tests;

public sealed class MetadataTranslationItemViewModelTests
{
    [Fact]
    public void StartsPending()
    {
        var item = new MetadataTranslationItemViewModel("ru");

        Assert.Equal("RU", item.Header);
        Assert.Equal("ru", item.LanguageCode);
        Assert.Equal("○", item.Marker);
        Assert.False(item.IsRunning);
        Assert.False(item.IsDone);
        Assert.False(item.HasStage);
    }

    [Fact]
    public void MarkRunning_SetsStageAndFlag()
    {
        var item = new MetadataTranslationItemViewModel("ru");

        item.MarkRunning("Translating…");

        Assert.Equal("▶", item.Marker);
        Assert.Equal("Translating…", item.Stage);
        Assert.True(item.HasStage);
        Assert.True(item.IsRunning);
        Assert.False(item.IsDone);
    }

    [Fact]
    public void MarkRunning_EmptyStage_FallsBackToEllipsis()
    {
        var item = new MetadataTranslationItemViewModel("ru");

        item.MarkRunning(string.Empty);

        Assert.Equal("…", item.Stage);
    }

    [Fact]
    public void MarkDone_SetsDoneAndClearsStage()
    {
        var item = new MetadataTranslationItemViewModel("ru");
        item.MarkRunning("Translating…");

        item.MarkDone();

        Assert.Equal("✓", item.Marker);
        Assert.Empty(item.Stage);
        Assert.True(item.IsDone);
        Assert.False(item.IsRunning);
    }

    [Fact]
    public void MarkPending_ClearsDone()
    {
        var item = new MetadataTranslationItemViewModel("ru");
        item.MarkDone();

        item.MarkPending();

        Assert.Equal("○", item.Marker);
        Assert.False(item.IsDone);
    }

    [Fact]
    public void MarkFailed_ShowsMessageAndStaysUndone()
    {
        var item = new MetadataTranslationItemViewModel("ru");
        item.MarkRunning("Translating…");

        item.MarkFailed("Provider unavailable");

        Assert.Equal("✗", item.Marker);
        Assert.Equal("Provider unavailable", item.Stage);
        Assert.False(item.IsDone);
        Assert.False(item.IsRunning);
    }
}
