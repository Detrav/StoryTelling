using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using StoryTelling.Domain;

namespace StoryTelling.Application.Export;

public static partial class Fb2Exporter
{
    private static readonly XNamespace _fb = "http://www.gribuser.ru/xml/fictionbook/2.0";

    public static string Build(Project project, string languageCode)
    {
        var bookTitle = ResolveBookTitle(project, languageCode);
        var annotation = ResolveAnnotation(project, languageCode);

        var titleInfo = new XElement(_fb + "title-info",
            new XElement(_fb + "genre", ResolveGenre(project)),
            new XElement(_fb + "author", new XElement(_fb + "nickname", "StoryTelling")),
            new XElement(_fb + "book-title", bookTitle));

        if (!string.IsNullOrWhiteSpace(annotation))
        {
            titleInfo.Add(new XElement(_fb + "annotation", new XElement(_fb + "p", annotation.Trim())));
        }

        titleInfo.Add(new XElement(_fb + "lang", ResolveLanguage(languageCode)));

        var description = new XElement(_fb + "description",
            titleInfo,
            new XElement(_fb + "document-info",
                new XElement(_fb + "author", new XElement(_fb + "nickname", "StoryTelling")),
                new XElement(_fb + "program-used", "StoryTelling"),
                new XElement(_fb + "date", project.UpdatedUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                new XElement(_fb + "id", project.Id.ToString()),
                new XElement(_fb + "version", "1.0")));

        var body = new XElement(_fb + "body",
            new XElement(_fb + "title", new XElement(_fb + "p", bookTitle)));

        foreach (var chapter in project.Chapters.OrderBy(chapter => chapter.Number))
        {
            var section = new XElement(_fb + "section",
                new XElement(_fb + "title", new XElement(_fb + "p", ResolveChapterTitle(chapter, languageCode))));

            foreach (var paragraph in SplitParagraphs(ResolveText(project, chapter, languageCode)))
            {
                section.Add(new XElement(_fb + "p", paragraph));
            }

            body.Add(section);
        }

        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(_fb + "FictionBook", description, body));

        var settings = new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) };
        using var writer = new Utf8StringWriter();
        using (var xml = XmlWriter.Create(writer, settings))
        {
            document.Save(xml);
        }

        return writer.ToString();
    }

    private static string ResolveText(Project project, Chapter chapter, string languageCode)
    {
        if (IsOriginal(languageCode))
        {
            return chapter.ContentOriginal;
        }

        return chapter.Translations.TryGetValue(languageCode, out var translated) && !string.IsNullOrWhiteSpace(translated)
            ? translated
            : chapter.ContentOriginal;
    }

    private static string ResolveBookTitle(Project project, string languageCode) =>
        !IsOriginal(languageCode)
        && project.MetadataTranslations.TryGetValue(languageCode, out var metadata)
        && !string.IsNullOrWhiteSpace(metadata.Name)
            ? metadata.Name.Trim()
            : project.Name ?? string.Empty;

    private static string ResolveAnnotation(Project project, string languageCode) =>
        !IsOriginal(languageCode)
        && project.MetadataTranslations.TryGetValue(languageCode, out var metadata)
        && !string.IsNullOrWhiteSpace(metadata.Annotation)
            ? metadata.Annotation.Trim()
            : project.World.Body ?? string.Empty;

    private static string ResolveChapterTitle(Chapter chapter, string languageCode)
    {
        if (!IsOriginal(languageCode)
            && chapter.TranslatedTitles.TryGetValue(languageCode, out var translated)
            && !string.IsNullOrWhiteSpace(translated))
        {
            return translated.Trim();
        }

        return string.IsNullOrWhiteSpace(chapter.Title) ? $"Chapter {chapter.Number}" : chapter.Title.Trim();
    }

    private static bool IsOriginal(string languageCode) =>
        string.Equals(languageCode, "en", StringComparison.OrdinalIgnoreCase);

    private static string ResolveGenre(Project project) =>
        string.IsNullOrWhiteSpace(project.World.Genre) ? "prose" : project.World.Genre.Trim();

    private static string ResolveLanguage(string languageCode) =>
        string.IsNullOrWhiteSpace(languageCode) ? "en" : languageCode.Trim().ToLowerInvariant();

    private static IEnumerable<string> SplitParagraphs(string text) =>
        [.. ParagraphSeparator().Split(text ?? string.Empty)
            .Select(block => InlineBreak().Replace(block.Trim(), " "))
            .Where(block => block.Length > 0)];

    [GeneratedRegex(@"\r?\n\s*\r?\n")]
    private static partial Regex ParagraphSeparator();

    [GeneratedRegex(@"\s*\r?\n\s*")]
    private static partial Regex InlineBreak();

    private sealed class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => new UTF8Encoding(false);
    }
}
