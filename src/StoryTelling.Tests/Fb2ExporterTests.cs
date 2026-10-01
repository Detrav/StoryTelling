using System.Linq;
using System.Xml.Linq;
using StoryTelling.Application.Export;
using StoryTelling.Domain;

namespace StoryTelling.Tests;

public sealed class Fb2ExporterTests
{
    private static readonly XNamespace _fb = "http://www.gribuser.ru/xml/fictionbook/2.0";

    [Fact]
    public void Build_English_UsesOriginalTextAndChapters()
    {
        var xml = Fb2Exporter.Build(Project(), "en");
        var document = XDocument.Parse(xml);

        Assert.Equal("The Ember Crown", document.Root?.Element(_fb + "description")?.Element(_fb + "title-info")?.Element(_fb + "book-title")?.Value);
        Assert.Equal(2, document.Root?.Element(_fb + "body")?.Elements(_fb + "section").Count());
        Assert.Contains("Original one.", xml);
        Assert.DoesNotContain("Перевод один.", xml);
    }

    [Fact]
    public void Build_Translated_UsesTranslations()
    {
        var xml = Fb2Exporter.Build(Project(), "ru");

        Assert.Contains("Перевод один.", xml);
        Assert.DoesNotContain("Original one.", xml);
        Assert.Contains("<lang>ru</lang>", xml);
    }

    [Fact]
    public void Build_UntranslatedChapter_FallsBackToOriginal()
    {
        var xml = Fb2Exporter.Build(Project(), "ru");

        Assert.Contains("Original two.", xml);
    }

    [Fact]
    public void Build_Translated_UsesTranslatedMetadata()
    {
        var xml = Fb2Exporter.Build(Project(), "ru");
        var document = XDocument.Parse(xml);

        Assert.Equal("Корона из углей", document.Root?.Element(_fb + "description")?.Element(_fb + "title-info")?.Element(_fb + "book-title")?.Value);
        Assert.Contains("Умирающая империя.", xml);
        Assert.Contains("Угли", xml);
        Assert.DoesNotContain("A dying empire.", xml);
    }

    [Fact]
    public void Build_UntranslatedMetadata_FallsBackToEnglish()
    {
        var project = Project();
        project.MetadataTranslations.Clear();
        foreach (var chapter in project.Chapters)
        {
            chapter.TranslatedTitles.Clear();
        }

        var xml = Fb2Exporter.Build(project, "ru");
        var document = XDocument.Parse(xml);

        Assert.Equal("The Ember Crown", document.Root?.Element(_fb + "description")?.Element(_fb + "title-info")?.Element(_fb + "book-title")?.Value);
        Assert.Contains("A dying empire.", xml);
        Assert.Contains("Embers", xml);
    }

    [Fact]
    public void Build_BlankChapterTitle_FallsBackToChapterNumber()
    {
        var project = Project();
        project.Chapters[0].Title = "   ";

        var xml = Fb2Exporter.Build(project, "en");
        var document = XDocument.Parse(xml);

        var title = document.Root!.Element(_fb + "body")!.Elements(_fb + "section").First().Element(_fb + "title");
        Assert.Equal("Chapter 1", title?.Value);
    }

    [Fact]
    public void Build_BlankGenre_FallsBackToProse()
    {
        var project = Project();
        project.World.Genre = "  ";

        var xml = Fb2Exporter.Build(project, "en");

        Assert.Contains("<genre>prose</genre>", xml);
    }

    [Fact]
    public void Build_BlankLanguage_FallsBackToEnglish()
    {
        var xml = Fb2Exporter.Build(Project(), "   ");

        Assert.Contains("<lang>en</lang>", xml);
    }

    [Fact]
    public void Build_BlankAnnotation_OmitsAnnotationElement()
    {
        var project = Project();
        project.World.Body = "   ";

        var xml = Fb2Exporter.Build(project, "en");

        Assert.DoesNotContain("<annotation>", xml);
    }

    [Fact]
    public void Build_MissingWorld_DoesNotThrow()
    {
        var project = Project();
        project.World = null!;

        var xml = Fb2Exporter.Build(project, "ru");

        Assert.Contains("<genre>prose</genre>", xml);
        Assert.Contains("<book-title>Корона из углей</book-title>", xml);
    }

    [Fact]
    public void Build_English_IgnoresTranslatedMetadata()
    {
        var xml = Fb2Exporter.Build(Project(), "en");
        var document = XDocument.Parse(xml);
        var titleInfo = document.Root!.Element(_fb + "description")!.Element(_fb + "title-info")!;

        Assert.Equal("The Ember Crown", titleInfo.Element(_fb + "book-title")?.Value);
        Assert.Equal("A dying empire.", titleInfo.Element(_fb + "annotation")?.Value);
        Assert.Equal("Embers", document.Root!.Element(_fb + "body")!.Elements(_fb + "section").First().Element(_fb + "title")?.Value);
    }

    [Fact]
    public void Build_EscapesSpecialCharacters()
    {
        var project = Project();
        project.Chapters[0].ContentOriginal = "A <tag> & \"quotes\" > end.";

        var xml = Fb2Exporter.Build(project, "en");
        var document = XDocument.Parse(xml);
        var section = document.Root!.Element(_fb + "body")!.Elements(_fb + "section").First();

        Assert.Contains("A <tag> & \"quotes\" > end.", section.Value);
    }

    private static Project Project() => new()
    {
        Name = "The Ember Crown",
        World = new World { Genre = "fantasy", Body = "A dying empire." },
        MetadataTranslations = new SortedDictionary<string, MetadataTranslation>
        {
            ["ru"] = new MetadataTranslation { Name = "Корона из углей", Annotation = "Умирающая империя." },
        },
        Chapters =
        [
            new Chapter
            {
                Number = 1,
                Title = "Embers",
                ContentOriginal = "Original one.",
                Translations = new SortedDictionary<string, string> { ["ru"] = "Перевод один." },
                TranslatedTitles = new SortedDictionary<string, string> { ["ru"] = "Угли" },
            },
            new Chapter { Number = 2, Title = "Ash", ContentOriginal = "Original two." },
        ],
    };
}
