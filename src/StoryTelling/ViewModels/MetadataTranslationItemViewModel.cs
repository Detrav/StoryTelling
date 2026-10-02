namespace StoryTelling.ViewModels;

public sealed class MetadataTranslationItemViewModel : ProgressItemViewModel
{
    public MetadataTranslationItemViewModel(string languageCode)
        : base(languageCode.ToUpperInvariant())
    {
        LanguageCode = languageCode;
    }

    public string LanguageCode { get; }
}
