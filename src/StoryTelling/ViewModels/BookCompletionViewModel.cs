using System.Collections.ObjectModel;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace StoryTelling.ViewModels;

public partial class BookCompletionViewModel : ViewModelBase
{
    public delegate Task CompletionRunner(
        IReadOnlyList<BookOperation> operations,
        IProgress<BookCompletionProgress> progress,
        CancellationToken cancellationToken);

    private readonly CompletionRunner _run;
    private CancellationTokenSource? _cts;

    public BookCompletionViewModel(Func<IReadOnlyList<BookOperation>> plan, CompletionRunner run)
    {
        _run = run;
        foreach (var operation in plan())
        {
            Operations.Add(new BookCompletionItemViewModel(operation));
        }
    }

    public event Action? CloseRequested;

    public ObservableCollection<BookCompletionItemViewModel> Operations { get; } = [];

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isFinished;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string _progressText = string.Empty;

    [ObservableProperty]
    private string _status = "Starting…";

    public async Task StartAsync()
    {
        if (Operations.Count == 0)
        {
            Status = "Everything is already up to date.";
            IsFinished = true;
            return;
        }

        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        IsBusy = true;
        IsFinished = false;
        Status = "Starting…";
        ProgressText = $"0 / {Operations.Count}";

        var progress = new Progress<BookCompletionProgress>(Apply);

        try
        {
            await _run([.. Operations.Select(item => item.Operation)], progress, token);
            if (!token.IsCancellationRequested)
            {
                Status = "Done.";
                Progress = 100;
                ProgressText = $"{Operations.Count} / {Operations.Count}";
                UpdateOperations(Operations.Count, -1, string.Empty);
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
            IsFinished = true;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _cts?.Cancel();
        CloseRequested?.Invoke();
    }

    public void CancelWork() => _cts?.Cancel();

    private void Apply(BookCompletionProgress report)
    {
        Progress = report.Total <= 0 ? 100 : Math.Min(100, report.Completed * 100.0 / report.Total);
        ProgressText = $"{report.Completed} / {report.Total}";

        if (report.CurrentIndex >= 0 && report.CurrentIndex < Operations.Count)
        {
            Status = Operations[report.CurrentIndex].Header;
        }

        UpdateOperations(report.Completed, report.CurrentIndex, report.Stage);
    }

    private void UpdateOperations(int completed, int currentIndex, string stage)
    {
        for (var index = 0; index < Operations.Count; index++)
        {
            var item = Operations[index];
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
        foreach (var item in Operations)
        {
            if (item.IsRunning)
            {
                item.MarkPending();
            }
        }
    }

    private void FailRunning(string message)
    {
        foreach (var item in Operations)
        {
            if (item.IsRunning)
            {
                item.MarkFailed(message);
            }
        }
    }
}