using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace StoryTelling.ViewModels;

public partial class MetadataTranslationViewModel : ViewModelBase
{
    public delegate Task TranslationRunner(
        IProgress<MetadataTranslationProgress> progress,
        CancellationToken cancellationToken);

    private readonly TranslationRunner _run;
    private CancellationTokenSource? _cts;

    public MetadataTranslationViewModel(
        IReadOnlyList<string> languages,
        Func<string, bool> needsTranslation,
        TranslationRunner run)
    {
        _run = run;
        foreach (var code in languages)
        {
            var item = new MetadataTranslationItemViewModel(code);
            if (!needsTranslation(code))
            {
                item.MarkDone();
            }

            Languages.Add(item);
        }
    }

    public event Action? CloseRequested;

    public ObservableCollection<MetadataTranslationItemViewModel> Languages { get; } = [];

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string _progressText = string.Empty;

    [ObservableProperty]
    private string _status = "Starting…";

    public async Task StartAsync()
    {
        if (Languages.Count == 0)
        {
            Status = "No target languages to translate.";
            return;
        }

        if (Languages.All(item => item.IsDone))
        {
            Status = "Book metadata is already up to date.";
            Progress = 100;
            ProgressText = $"{Languages.Count} / {Languages.Count}";
            return;
        }

        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        IsBusy = true;
        Status = "Starting…";
        ProgressText = $"0 / {Languages.Count}";

        var progress = new Progress<MetadataTranslationProgress>(Apply);

        try
        {
            await _run(progress, token);
            if (!token.IsCancellationRequested)
            {
                Status = "Done.";
                Progress = 100;
                ProgressText = $"{Languages.Count} / {Languages.Count}";
                UpdateItems(Languages.Count, -1, string.Empty);
            }
        }
        catch (OperationCanceledException)
        {
            Status = "Cancelled.";
            CancelRunning();
        }
        catch (Exception exception)
        {
            Status = $"Failed: {exception.Message}";
            FailRunning(exception.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _cts?.Cancel();
        CloseRequested?.Invoke();
    }

    public void CancelWork() => _cts?.Cancel();

    private void Apply(MetadataTranslationProgress report)
    {
        Progress = report.Total <= 0 ? 100 : Math.Min(100, report.Completed * 100.0 / report.Total);
        ProgressText = $"{report.Completed} / {report.Total}";
        UpdateItems(report.Completed, report.CurrentIndex, report.Stage);
    }

    private void UpdateItems(int completed, int currentIndex, string stage)
    {
        for (var index = 0; index < Languages.Count; index++)
        {
            var item = Languages[index];
            if (index == currentIndex)
            {
                item.MarkRunning(stage);
            }
            else if (index < completed)
            {
                item.MarkDone();
            }
            else
            {
                item.MarkPending();
            }
        }
    }

    private void CancelRunning()
    {
        foreach (var item in Languages)
        {
            if (item.IsRunning)
            {
                item.MarkPending();
            }
        }
    }

    private void FailRunning(string message)
    {
        foreach (var item in Languages)
        {
            if (item.IsRunning)
            {
                item.MarkFailed(message);
            }
        }
    }
}
