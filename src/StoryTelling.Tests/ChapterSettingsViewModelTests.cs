using StoryTelling.Domain;
using StoryTelling.ViewModels;

namespace StoryTelling.Tests;

public sealed class ChapterSettingsViewModelTests
{
    [Fact]
    public void TranslatedTitles_AreExposedForDisplay()
    {
        var chapter = new ChapterViewModel
        {
            TranslatedTitles = new SortedDictionary<string, string> { ["ru"] = "Угли", ["de"] = string.Empty },
        };
        var viewModel = new ChapterSettingsViewModel(chapter, () => { });

        var title = Assert.Single(viewModel.TranslatedTitles);
        Assert.Equal("RU", title.LanguageCode);
        Assert.Equal("Угли", title.Title);
        Assert.True(viewModel.HasTranslatedTitles);
    }

    [Fact]
    public void Role_WritesThroughToTheChapter()
    {
        var chapter = new ChapterViewModel();
        var viewModel = new ChapterSettingsViewModel(chapter, () => { });

        viewModel.Role = ChapterRole.Finale;

        Assert.Equal(ChapterRole.Finale, chapter.Role);
    }

    [Fact]
    public void HasStaleTranslations_TracksTheList()
    {
        var chapter = new ChapterViewModel();
        Assert.False(chapter.HasStaleTranslations);

        chapter.StaleTranslations = ["ru"];

        Assert.True(chapter.HasStaleTranslations);
    }
}
