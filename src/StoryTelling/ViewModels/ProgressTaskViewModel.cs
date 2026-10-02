using System.Collections.ObjectModel;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace StoryTelling.ViewModels;

public partial class ProgressTaskViewModel : ViewModelBase
{
    public delegate Task Runner(IProgress<ProgressTaskProgress> progress, CancellationToken cancellationToken);

    private readonly Runner _run;
    private readonly object _gate = new();
    private CancellationTokenSource? _cts;
    private bool _finished;

    public ProgressTaskViewModel(string title, string description, IReadOnlyList<ProgressItemViewModel> items, Runner run)
    {
        Title = title;
        Description = description;
        _run = run;

        foreach (var item in items)
        {
            Items.Add(item);
        }
    }

    public event Action? CloseRequested;

    public string Title { get; }

    public string Description { get; }

    public bool HasDescription => Description.Length > 0;

    public ObservableCollection<ProgressItemViewModel> Items { get; } = [];

    public bool HasProgress => Items.Count > 1;

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
        _finished = false;

        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        IsBusy = true;
        Progress = 0;
        ProgressText = $"0 / {Items.Count}";
        var progress = new Progress<ProgressTaskProgress>(Apply);

        try
        {
            await _run(progress, token);
            if (token.IsCancellationRequested)
            {
                return;
            }

            Finish(() =>
            {
                Status = "Done.";
                Progress = 100;
                ProgressText = $"{Items.Count} / {Items.Count}";
                ProgressItems.MarkAllDone(Items);
            });
        }
        catch (OperationCanceledException)
        {
            Finish(() =>
            {
                Status = "Cancelled.";
                ProgressItems.CancelRunning(Items);
            });
        }
        catch (Exception exception)
        {
            Finish(() =>
            {
                Status = $"Failed: {exception.Message}";
                ProgressItems.FailRunning(Items, exception.Message);
            });
        }
        finally
        {
            Finish(() => IsBusy = false);
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _cts?.Cancel();
        CloseRequested?.Invoke();
    }

    public void CancelWork() => _cts?.Cancel();

    private void Finish(Action apply)
    {
        lock (_gate)
        {
            _finished = true;
            apply();
        }
    }

    private void Apply(ProgressTaskProgress report)
    {
        lock (_gate)
        {
            if (_finished)
            {
                return;
            }

            Progress = report.Total <= 0 ? 100 : Math.Min(100, report.Completed * 100.0 / report.Total);
            ProgressText = $"{report.Completed} / {report.Total}";

            if (report.CurrentIndex >= 0 && report.CurrentIndex < Items.Count)
            {
                Status = Items[report.CurrentIndex].Header;
            }

            ProgressItems.Update(Items, report.Completed, report.CurrentIndex, report.Stage);
        }
    }
}
