using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StoryTelling.Application.Generation;

namespace StoryTelling.ViewModels;

public partial class ChapterTextViewModel : ViewModelBase
{
    private readonly Action<string> _apply;
    private readonly CommitDebouncer _debouncer;
    private bool _streaming;
    private CancellationTokenSource? _cts;

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

    public Func<IProgress<GenerationProgress>?, CancellationToken, Task<string>>? Translate { get; set; }

    [ObservableProperty]
    private string _text;

    [ObservableProperty]
    private bool _isStale;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _status = string.Empty;

    [RelayCommand]
    private async Task TranslateAsync()
    {
        if (Translate is not { } translate || IsBusy)
        {
            return;
        }

        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        IsBusy = true;
        Status = "Translating…";

        var progress = new Progress<GenerationProgress>(report =>
            Status = report.ToolCalls > 0 ? $"{report.Stage}… ({report.ToolCalls} tool calls)" : $"{report.Stage}…");

        try
        {
            var translated = await translate(progress, token);
            Text = translated;
            IsStale = false;
            Status = "Translated.";
        }
        catch (OperationCanceledException)
        {
            Status = "Stopped.";
        }
        catch (Exception exception)
        {
            Status = $"Failed: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void StopTranslate() => _cts?.Cancel();

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
