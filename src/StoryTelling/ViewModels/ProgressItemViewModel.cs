using CommunityToolkit.Mvvm.ComponentModel;

namespace StoryTelling.ViewModels;

public partial class ProgressItemViewModel : ObservableObject
{
    private readonly bool _isSkip;
    private readonly string _skipReason;

    public ProgressItemViewModel(string header, string? skipReason = null)
    {
        Header = header;
        _isSkip = skipReason is not null;
        _skipReason = skipReason ?? string.Empty;
        _marker = _isSkip ? "⚠" : "○";
        _stage = _skipReason;
    }

    public string Header { get; }

    [ObservableProperty]
    private string _marker;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStage))]
    private string _stage;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _isDone;

    public bool HasStage => Stage.Length > 0;

    public void MarkPending()
    {
        Marker = _isSkip ? "⚠" : "○";
        Stage = _skipReason;
        IsRunning = false;
        IsDone = false;
    }

    public void MarkRunning(string stage)
    {
        Marker = "▶";
        Stage = string.IsNullOrEmpty(stage) ? "…" : stage;
        IsRunning = true;
        IsDone = false;
    }

    public void MarkDone()
    {
        if (_isSkip)
        {
            MarkPending();
            return;
        }

        Marker = "✓";
        Stage = string.Empty;
        IsRunning = false;
        IsDone = true;
    }

    public void MarkFailed(string message)
    {
        Marker = "✗";
        Stage = message;
        IsRunning = false;
        IsDone = false;
    }
}
