namespace StoryTelling.ViewModels;

public sealed record BookOperation(
    BookOperationKind Kind,
    int ChapterNumber,
    string Header,
    string? LanguageCode = null,
    string? SkipReason = null);