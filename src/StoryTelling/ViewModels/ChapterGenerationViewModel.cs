using System.Collections.ObjectModel;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StoryTelling.Application.Generation;

namespace StoryTelling.ViewModels;

public partial class ChapterGenerationViewModel : ViewModelBase
{
    public delegate Task Runner(IProgress<GenerationProgress> progress, CancellationToken cancellationToken);

    private readonly Runner _run;
    private readonly object _gate = new();
    private CancellationTokenSource? _cts;
    private bool _finished;
    private int _step = -1;

    public ChapterGenerationViewModel(Runner run)
    {
        _run = run;
        Steps.Add(new ProgressItemViewModel("Gather context"));
        Steps.Add(new ProgressItemViewModel("Write draft"));
        Steps.Add(new ProgressItemViewModel("Edit draft"));
        Steps.Add(new ProgressItemViewModel("Summarize and update the story state"));
    }

    public event Action? CloseRequested;

    public string Title => "Generate chapter";

    public string Description => "The AI gathers context, writes a draft, edits it, then updates the story state.";

    public ObservableCollection<ProgressItemViewModel> Steps { get; } = [];

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _canClose;

    [ObservableProperty]
    private string _status = "Starting…";

    [ObservableProperty]
    private string _detail = string.Empty;

    public bool HasDetail => Detail.Length > 0;

    public async Task StartAsync()
    {
        _finished = false;

        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        IsBusy = true;
        var progress = new Progress<GenerationProgress>(Apply);

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
                Detail = "Close to read the chapter.";
                ProgressItems.MarkAllDone(Steps);
                CanClose = true;
            });
        }
        catch (OperationCanceledException)
        {
            Finish(() =>
            {
                Status = "Cancelled.";
                Detail = "The chapter was left unchanged.";
                ProgressItems.CancelRunning(Steps);
                CanClose = true;
            });
        }
        catch (Exception exception)
        {
            Finish(() =>
            {
                Status = $"Failed: {exception.Message}";
                Detail = "The chapter was left unchanged.";
                ProgressItems.FailRunning(Steps, exception.Message);
                CanClose = true;
            });
        }
        finally
        {
            Finish(() => IsBusy = false);
        }
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke();

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

    private void Apply(GenerationProgress report)
    {
        lock (_gate)
        {
            if (_finished)
            {
                return;
            }

            _step = Math.Max(_step, MapStep(report.Stage, _step));
            Status = report.ToolCalls > 0
                ? $"{report.Stage}… ({report.ToolCalls} tool calls)"
                : $"{report.Stage}…";
            ProgressItems.Update(Steps, _step, _step, Status);
        }
    }

    public static int MapStep(string stage, int current) => stage switch
    {
        "Writing" => 1,
        "Editing" => 2,
        "Summarizing" => 3,
        "Gathering context" => current >= 1 ? 2 : 0,
        _ => current,
    };

    partial void OnDetailChanged(string value) => OnPropertyChanged(nameof(HasDetail));
}
