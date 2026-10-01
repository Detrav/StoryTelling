using StoryTelling.Application.Translation;
using StoryTelling.Domain;

namespace StoryTelling.Tests;

public sealed class MetadataTranslationCoverageTests
{
    [Fact]
    public void Evaluate_English_IsAlwaysComplete()
    {
        var coverage = MetadataTranslationCoverage.Evaluate(new Project(), "en");

        Assert.True(coverage.IsComplete);
        Assert.False(coverage.IsStale);
    }

    [Fact]
    public void Evaluate_MissingCache_ReportsNothingTranslated()
    {
        var coverage = MetadataTranslationCoverage.Evaluate(Project(), "ru");

        Assert.False(coverage.HasBookTitle);
        Assert.False(coverage.HasAnnotation);
        Assert.Equal(0, coverage.TranslatedTitles);
        Assert.Equal(2, coverage.TotalTitles);
        Assert.False(coverage.IsComplete);
    }

    [Fact]
    public void Evaluate_FullCache_IsComplete()
    {
        var coverage = MetadataTranslationCoverage.Evaluate(TranslatedProject(), "ru");

        Assert.True(coverage.HasBookTitle);
        Assert.True(coverage.HasAnnotation);
        Assert.Equal(2, coverage.TranslatedTitles);
        Assert.Equal(2, coverage.TotalTitles);
        Assert.False(coverage.IsStale);
        Assert.True(coverage.IsComplete);
    }

    [Fact]
    public void Evaluate_StaleLanguage_IsIncomplete()
    {
        var project = TranslatedProject();
        project.StaleMetadataTranslations = ["ru"];

        var coverage = MetadataTranslationCoverage.Evaluate(project, "ru");

        Assert.True(coverage.HasBookTitle);
        Assert.True(coverage.IsStale);
        Assert.False(coverage.IsComplete);
    }

    [Fact]
    public void Evaluate_StaleFlag_IgnoresLanguageCase()
    {
        var project = TranslatedProject();
        project.StaleMetadataTranslations = ["RU"];

        Assert.True(MetadataTranslationCoverage.Evaluate(project, "ru").IsStale);
    }

    [Fact]
    public void Evaluate_PartialTitles_CountsOnlyTranslatedOnes()
    {
        var project = TranslatedProject();
        project.Chapters[1].TranslatedTitles.Remove("ru");

        var coverage = MetadataTranslationCoverage.Evaluate(project, "ru");

        Assert.Equal(1, coverage.TranslatedTitles);
        Assert.Equal(2, coverage.TotalTitles);
        Assert.False(coverage.IsComplete);
    }

    [Fact]
    public void Evaluate_BlankCachedTitle_DoesNotCount()
    {
        var project = TranslatedProject();
        project.Chapters[0].TranslatedTitles["ru"] = "   ";

        Assert.Equal(1, MetadataTranslationCoverage.Evaluate(project, "ru").TranslatedTitles);
    }

    [Fact]
    public void Evaluate_ChaptersWithoutTitle_AreNotCounted()
    {
        var project = TranslatedProject();
        project.Chapters.Add(new Chapter { Number = 3, Title = "  ", ContentOriginal = "Text." });

        Assert.Equal(2, MetadataTranslationCoverage.Evaluate(project, "ru").TotalTitles);
    }

    [Fact]
    public void Evaluate_BlankWorldBody_DoesNotRequireAnnotation()
    {
        var project = Project();
        project.World.Body = string.Empty;

        var coverage = MetadataTranslationCoverage.Evaluate(project, "ru");

        Assert.True(coverage.HasAnnotation);
        Assert.False(coverage.HasBookTitle);
    }

    [Fact]
    public void Evaluate_BlankAnnotationInCache_IsIncomplete()
    {
        var project = TranslatedProject();
        project.MetadataTranslations["ru"] = new MetadataTranslation { Name = "Корона", Annotation = "   " };

        var coverage = MetadataTranslationCoverage.Evaluate(project, "ru");

        Assert.False(coverage.HasAnnotation);
        Assert.False(coverage.IsComplete);
    }

    [Fact]
    public void Evaluate_MissingWorld_DoesNotRequireAnnotation()
    {
        var project = Project();
        project.World = null!;

        Assert.True(MetadataTranslationCoverage.Evaluate(project, "ru").HasAnnotation);
    }

    [Fact]
    public void Evaluate_BlankProjectName_StaysCompleteSoTheModelIsNotCalledAgain()
    {
        var project = TranslatedProject();
        project.Name = string.Empty;
        project.MetadataTranslations["ru"] = new MetadataTranslation { Name = string.Empty, Annotation = "Аннотация" };

        var coverage = MetadataTranslationCoverage.Evaluate(project, "ru");

        Assert.True(coverage.HasBookTitle);
        Assert.True(coverage.HasAnnotation);
        Assert.Equal(2, coverage.TranslatedTitles);
        Assert.True(coverage.IsComplete);
    }

    private static Project Project() => new()
    {
        Name = "The Ember Crown",
        World = new World { Body = "A dying empire." },
        Chapters =
        [
            new Chapter { Number = 1, Title = "Embers", ContentOriginal = "One." },
            new Chapter { Number = 2, Title = "Ash", ContentOriginal = "Two." },
        ],
    };

    private static Project TranslatedProject()
    {
        var project = Project();
        project.MetadataTranslations["ru"] = new MetadataTranslation { Name = "Корона", Annotation = "Аннотация" };
        project.Chapters[0].TranslatedTitles["ru"] = "Угли";
        project.Chapters[1].TranslatedTitles["ru"] = "Пепел";
        return project;
    }
}
