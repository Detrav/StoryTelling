namespace StoryTelling.Application.Translation;

public sealed record MetadataTranslationResult(
    string BookName,
    string Annotation,
    IReadOnlyDictionary<int, string> ChapterTitles);