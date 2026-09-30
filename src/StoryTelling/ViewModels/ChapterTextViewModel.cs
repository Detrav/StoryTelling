using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace StoryTelling.ViewModels;

public partial class ChapterTextViewModel : ViewModelBase
{
    private readonly Action<string> _apply;
    private readonly CommitDebouncer _debouncer;

    public ChapterTextViewModel(string header, string text, bool isTranslation, Action<string> apply, Action commit)
    {
        Header = header;
        IsTranslation = isTranslation;
        _text = text;
        _apply = apply;
        _debouncer = new CommitDebouncer(commit, TimeSpan.FromMilliseconds(700));
    }

    public string Header { get; }

    public bool IsTranslation { get; }

    [ObservableProperty]
    private string _text;

    [RelayCommand]
    private void Translate() => Text = $"(mock) translated into {Header}.";

    public void Commit() => _debouncer.CommitNow();

    partial void OnTextChanged(string value)
    {
        _apply(value);
        _debouncer.Trigger();
    }
}
