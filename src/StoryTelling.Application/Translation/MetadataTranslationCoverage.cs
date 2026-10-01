using StoryTelling.Domain;

namespace StoryTelling.Application.Translation;

public sealed record MetadataCoverage(
    bool HasBookTitle,
    bool HasAnnotation,
    int TranslatedTitles,
    int TotalTitles,
    bool IsStale)
{
    public bool IsComplete => HasBookTitle && HasAnnotation && TranslatedTitles >= TotalTitles && !IsStale;
}

public static class MetadataTranslationCoverage
{
    public static MetadataCoverage Evaluate(Project project, string languageCode)
    {
        if (string.Equals(languageCode, "en", StringComparison.OrdinalIgnoreCase))
        {
            return new MetadataCoverage(true, true, 0, 0, false);
        }

        var cached = project.MetadataTranslations.TryGetValue(languageCode, out var translation);
        var hasBookTitle = cached && !string.IsNullOrWhiteSpace(translation!.Name);
        var hasAnnotation = string.IsNullOrWhiteSpace(project.World.Body)
            || (cached && !string.IsNullOrWhiteSpace(translation!.Annotation));

        var titled = project.Chapters
            .Where(chapter => !string.IsNullOrWhiteSpace(chapter.Title))
            .ToList();
        var translatedTitles = titled.Count(chapter =>
            chapter.TranslatedTitles.TryGetValue(languageCode, out var title) && !string.IsNullOrWhiteSpace(title));

        return new MetadataCoverage(
            hasBookTitle,
            hasAnnotation,
            translatedTitles,
            titled.Count,
            project.StaleMetadataTranslations.Contains(languageCode));
    }
}
