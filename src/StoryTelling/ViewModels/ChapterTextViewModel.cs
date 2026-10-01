using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace StoryTelling.ViewModels;

public partial class ChapterTextViewModel : ViewModelBase
{
    private readonly Action<string> _apply;
    private readonly CommitDebouncer _debouncer;
    private bool _streaming;

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

    public void BeginStream()
    {
        _streaming = true;
        Text = string.Empty;
    }

    public void AppendStreaming(string delta) => Text += delta;

    public void EndStream(string finalText)
    {
        Text = finalText;
        _streaming = false;
        _apply(finalText);
    }

    public void Commit() => _debouncer.CommitNow();

    partial void OnTextChanged(string value)
    {
        if (_streaming)
        {
            return;
        }

        _apply(value);
        _debouncer.Trigger();
    }
}
