using CommunityToolkit.Mvvm.ComponentModel;

namespace StoryTelling.ViewModels;

public partial class TranslationViewModel : ObservableObject
{
    public TranslationViewModel(string languageCode, string text)
    {
        LanguageCode = languageCode;
        _text = text;
    }

    public string LanguageCode { get; }

    public string Header => $"Chapter ({LanguageCode.ToUpperInvariant()})";

    [ObservableProperty]
    private string _text;
}
