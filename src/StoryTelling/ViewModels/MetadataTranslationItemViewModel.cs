using CommunityToolkit.Mvvm.ComponentModel;

namespace StoryTelling.ViewModels;

public partial class MetadataTranslationItemViewModel : ObservableObject
{
    public MetadataTranslationItemViewModel(string languageCode)
    {
        LanguageCode = languageCode;
    }

    public string LanguageCode { get; }

    public string Header => LanguageCode.ToUpperInvariant();

    [ObservableProperty]
    private string _marker = "○";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStage))]
    private string _stage = string.Empty;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _isDone;

    public bool HasStage => Stage.Length > 0;

    public void MarkPending()
    {
        Marker = "○";
        Stage = string.Empty;
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
