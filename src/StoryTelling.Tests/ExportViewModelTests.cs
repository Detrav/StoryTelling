using StoryTelling.Application.Settings;
using StoryTelling.Application.Translation;
using StoryTelling.Domain;
using StoryTelling.ViewModels;

namespace StoryTelling.Tests;

public sealed class ExportViewModelTests
{
    [Fact]
    public void Coverage_Original()
    {
        var language = new ExportLanguage("en", "English (original)", 2, 2, new MetadataCoverage(true, true, 0, 0, false));

        Assert.True(language.IsOriginal);
        Assert.Equal("original", language.Coverage);
        Assert.Equal("original", language.MetadataCoverage);
        Assert.True(language.MetadataComplete);
        Assert.False(language.MetadataOnlyStale);
    }

    [Fact]
    public void Coverage_NoChapters()
    {
        var language = new ExportLanguage("ru", "Russian", 0, 0, new MetadataCoverage(true, true, 0, 0, false));

        Assert.Equal("no chapters", language.Coverage);
    }

    [Fact]
    public void Coverage_TranslatedRatio()
    {
        var language = new ExportLanguage("ru", "Russian", 1, 2, new MetadataCoverage(true, true, 2, 2, false));

        Assert.Equal("1/2 translated", language.Coverage);
        Assert.Equal("metadata complete", language.MetadataCoverage);
        Assert.True(language.MetadataComplete);
    }

    [Fact]
    public void MetadataCoverage_ListsEveryMissingPartInOrder()
    {
        var language = new ExportLanguage(
            "ru",
            "Russian",
            2,
            2,
            new MetadataCoverage(false, false, 8, 10, true));

        Assert.Equal("needs: book title, annotation, chapter titles 8/10, out of date", language.MetadataCoverage);
        Assert.False(language.MetadataComplete);
        Assert.False(language.MetadataOnlyStale);
    }

    [Fact]
    public void MetadataCoverage_OnlyStale_IsDetectedAsSuch()
    {
        var language = new ExportLanguage(
            "ru",
            "Russian",
            2,
            2,
            new MetadataCoverage(true, true, 2, 2, true));

        Assert.Equal("needs: out of date", language.MetadataCoverage);
        Assert.False(language.MetadataComplete);
        Assert.True(language.MetadataOnlyStale);
    }

    [Fact]
    public void BuildsLanguages_WithEnglishFirstAndSkipsDuplicateEnglish()
    {
        var project = new Project
        {
            Name = "The Ember Crown",
            Settings = new StorySettings { TargetLanguages = ["en", "ru", "de"] },
            World = new World { Body = "A dying empire." },
            Chapters =
            [
                new Chapter { Number = 1, Title = "Embers", ContentOriginal = "One.", Translations = new SortedDictionary<string, string> { ["ru"] = "Раз." } },
                new Chapter { Number = 2, Title = "Ash", ContentOriginal = "Two." },
            ],
        };

        var viewModel = new ExportViewModel(project, [new LanguageData("ru", "Russian")]);

        Assert.Equal(["en", "ru", "de"], viewModel.Languages.Select(language => language.Code));
        Assert.Equal("English (original)", viewModel.Languages[0].DisplayName);
        Assert.Equal("Russian (RU)", viewModel.Languages[1].DisplayName);
        Assert.Equal("DE", viewModel.Languages[2].DisplayName);
        Assert.Equal(1, viewModel.Languages[1].Translated);
        Assert.Equal(2, viewModel.Languages[1].Total);
        Assert.False(viewModel.Languages[1].MetadataComplete);
        Assert.True(viewModel.HasSelection);
        Assert.Same(viewModel.Languages[0], viewModel.Selected);
    }

    [Fact]
    public void HasSelection_TracksSelection()
    {
        var viewModel = new ExportViewModel(new Project(), []);

        Assert.True(viewModel.HasSelection);

        viewModel.Selected = null;

        Assert.False(viewModel.HasSelection);
    }
}
