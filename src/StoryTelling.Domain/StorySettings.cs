namespace StoryTelling.Domain;

public sealed class StorySettings
{
    public const string OriginalLanguageCode = "en";

    public string OriginalLanguage { get; set; } = OriginalLanguageCode;

    public string TargetLanguage { get; set; } = OriginalLanguageCode;
}
