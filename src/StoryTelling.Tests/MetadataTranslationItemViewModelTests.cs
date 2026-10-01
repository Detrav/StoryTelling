using System.Collections.Generic;
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
        Assert.False(item.IsRunning);
        Assert.False(item.IsDone);
        Assert.False(item.HasStage);
    }

    [Fact]
    public void MarkRunning_SetsStageAndFlag()
    {
        var item = new MetadataTranslationItemViewModel("ru");

        item.MarkRunning("Translating…");

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
    public void MarkDone_NotifiesThatIsDoneChanged()
    {
        var item = new MetadataTranslationItemViewModel("ru");
        var notifications = new List<string?>();
        item.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        item.MarkDone();

        Assert.True(item.IsDone);
        Assert.Contains(nameof(MetadataTranslationItemViewModel.IsDone), notifications);
    }

    [Fact]
    public void MarkPending_ClearsDoneAndNotifies()
    {
        var item = new MetadataTranslationItemViewModel("ru");
        item.MarkDone();
        var notifications = new List<string?>();
        item.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        item.MarkPending();

        Assert.False(item.IsDone);
        Assert.Contains(nameof(MetadataTranslationItemViewModel.IsDone), notifications);
    }

    [Fact]
    public void MarkFailed_ShowsMessageAndStaysUndone()
    {
        var item = new MetadataTranslationItemViewModel("ru");
        item.MarkRunning("Translating…");

        item.MarkFailed("Provider unavailable");

        Assert.Equal("Provider unavailable", item.Stage);
        Assert.False(item.IsDone);
        Assert.False(item.IsRunning);
    }
}
