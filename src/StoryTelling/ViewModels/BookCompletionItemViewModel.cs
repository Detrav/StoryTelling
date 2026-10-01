using CommunityToolkit.Mvvm.ComponentModel;

namespace StoryTelling.ViewModels;

public partial class BookCompletionItemViewModel : ObservableObject
{
    private readonly bool _isSkip;
    private readonly string _skipReason;

    public BookCompletionItemViewModel(BookOperation operation)
    {
        Operation = operation;
        _isSkip = operation.Kind == BookOperationKind.Skip;
        _skipReason = operation.SkipReason ?? string.Empty;
        _marker = _isSkip ? "⚠" : "○";
        _stage = _skipReason;
    }

    public BookOperation Operation { get; }

    public string Header => Operation.Header;

    [ObservableProperty]
    private string _marker;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStage))]
    private string _stage;

    [ObservableProperty]
    private bool _isRunning;

    public bool HasStage => Stage.Length > 0;

    public void MarkPending()
    {
        Marker = _isSkip ? "⚠" : "○";
        Stage = _skipReason;
        IsRunning = false;
    }

    public void MarkRunning(string stage)
    {
        Marker = "▶";
        Stage = string.IsNullOrEmpty(stage) ? "…" : stage;
        IsRunning = true;
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
    }

    public void MarkFailed(string message)
    {
        Marker = "✗";
        Stage = message;
        IsRunning = false;
    }
}