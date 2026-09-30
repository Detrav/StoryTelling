using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace StoryTelling.ViewModels;

public partial class ChapterTextViewModel : ViewModelBase
{
    private readonly Action<string> _apply;

    public ChapterTextViewModel(string header, string text, bool isTranslation, Action<string> apply)
    {
        Header = header;
        IsTranslation = isTranslation;
        _text = text;
        _apply = apply;
    }

    public string Header { get; }

    public bool IsTranslation { get; }

    [ObservableProperty]
    private string _text;

    [RelayCommand]
    private void Translate() => Text = $"(mock) translated into {Header}.";

    partial void OnTextChanged(string value) => _apply(value);
}
