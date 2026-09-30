using CommunityToolkit.Mvvm.ComponentModel;

namespace StoryTelling.ViewModels;

public partial class ChapterTranslation : ObservableObject
{
    public ChapterTranslation(string code, string text)
    {
        Code = code;
        _text = text;
    }

    public string Code { get; }

    public string Header => $"Chapter ({Code.ToUpperInvariant()})";

    [ObservableProperty]
    private string _text;
}
