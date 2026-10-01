namespace StoryTelling.Application.Translation;

public sealed record MetadataTranslationRequest(
    string LanguageCode,
    string BookName,
    string Annotation,
    IReadOnlyList<MetadataChapterTitle> ChapterTitles);