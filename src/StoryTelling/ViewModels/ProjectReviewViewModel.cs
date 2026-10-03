using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StoryTelling.Application.Generation;
using StoryTelling.Application.Llm;
using StoryTelling.Application.Review;

namespace StoryTelling.ViewModels;

public partial class ProjectReviewViewModel : ViewModelBase
{
    public delegate Task<IReadOnlyList<ReviewFinding>> RunCheck(
        ReviewCheck check,
        string brief,
        IProgress<GenerationProgress>? progress,
        CancellationToken cancellationToken);

    private readonly RunCheck _run;
    private readonly IReviewFixHost _host;
    private CancellationTokenSource? _cts;

    public ProjectReviewViewModel(IReadOnlyList<ReviewCheck> checks, RunCheck run, IReviewFixHost host)
    {
        _run = run;
        _host = host;
        Steps = new ObservableCollection<ReviewStepViewModel>(checks.Select(check => new ReviewStepViewModel(check)));
        _currentStep = Steps.FirstOrDefault();
    }

    public ObservableCollection<ReviewStepViewModel> Steps { get; }

    [ObservableProperty]
    private ReviewStepViewModel? _currentStep;

    [ObservableProperty]
    private ReviewFindingViewModel? _selectedFinding;

    [ObservableProperty]
    private int _stepIndex;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanApplySelected))]
    private bool _isApplying;

    [ObservableProperty]
    private bool _isFinished;

    [ObservableProperty]
    private string _brief = string.Empty;

    [ObservableProperty]
    private string _status = string.Empty;

    public bool HasSteps => Steps.Count > 0;

    public bool HasPrevious => StepIndex > 0 && !IsFinished;

    public bool ShowFindings => !IsFinished && CurrentStep is not null;

    public bool ShowDetail => SelectedFinding is not null;

    public string ProgressLabel => IsFinished ? "Done" : $"Step {StepIndex + 1} of {Steps.Count}";

    public string StepTitle => IsFinished ? "Summary" : CurrentStep?.Label ?? string.Empty;

    public string ApplySelectedLabel =>
        $"Apply selected ({CurrentStep?.Findings.Count(IsActionable) ?? 0})";

    public bool CanApplySelected =>
        !IsBusy && !IsApplying && CurrentStep is not null && CurrentStep.Findings.Any(IsActionable);

    private static bool IsActionable(ReviewFindingViewModel finding) =>
        finding.IsSelected && !finding.IsApplied && finding.Action != ReviewFindingAction.None;

    partial void OnCurrentStepChanged(ReviewStepViewModel? value)
    {
        if (value is not null)
        {
            var index = Steps.IndexOf(value);
            if (index >= 0)
            {
                StepIndex = index;
            }
        }

        SelectedFinding = value?.Findings.FirstOrDefault();
        OnPropertyChanged(nameof(StepTitle));
        OnPropertyChanged(nameof(ShowFindings));
        OnPropertyChanged(nameof(ApplySelectedLabel));
        OnPropertyChanged(nameof(CanApplySelected));
    }

    partial void OnSelectedFindingChanged(ReviewFindingViewModel? value) => OnPropertyChanged(nameof(ShowDetail));

    partial void OnStepIndexChanged(int value)
    {
        OnPropertyChanged(nameof(ProgressLabel));
        OnPropertyChanged(nameof(HasPrevious));
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanApplySelected));
        OnPropertyChanged(nameof(ApplySelectedLabel));
    }

    partial void OnIsFinishedChanged(bool value)
    {
        OnPropertyChanged(nameof(ProgressLabel));
        OnPropertyChanged(nameof(StepTitle));
        OnPropertyChanged(nameof(ShowFindings));
        OnPropertyChanged(nameof(HasPrevious));
        OnPropertyChanged(nameof(CanApplySelected));
    }

    public Task StartAsync() => RunCurrentStepAsync();

    public void Cancel() => _cts?.Cancel();

    [RelayCommand]
    private Task RunStep() => RunCurrentStepAsync();

    [RelayCommand]
    private void Stop() => Cancel();

    [RelayCommand]
    private void SelectAllFindings()
    {
        if (CurrentStep is null)
        {
            return;
        }

        foreach (var finding in CurrentStep.Findings)
        {
            finding.IsSelected = true;
        }
    }

    [RelayCommand]
    private void ClearFindingSelection()
    {
        if (CurrentStep is null)
        {
            return;
        }

        foreach (var finding in CurrentStep.Findings)
        {
            finding.IsSelected = false;
        }
    }

    [RelayCommand]
    private async Task ApplySelectedAsync()
    {
        if (CurrentStep is null || IsBusy || IsApplying)
        {
            return;
        }

        var targets = CurrentStep.Findings.Where(IsActionable).ToList();
        if (targets.Count == 0)
        {
            Status = "No findings selected.";
            return;
        }

        IsApplying = true;
        Status = "Applying…";
        var applied = 0;

        try
        {
            foreach (var finding in targets)
            {
                if (!finding.IsPrepared)
                {
                    await finding.PrepareAsync();
                }

                if (finding.IsPrepared && !finding.IsApplied)
                {
                    await finding.ApplyAsync();
                    applied++;
                }
            }

            Status = $"Applied {applied} finding(s).";
        }
        finally
        {
            IsApplying = false;
        }
    }

    [RelayCommand]
    private async Task NextAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (StepIndex + 1 >= Steps.Count)
        {
            IsFinished = true;
            CurrentStep = null;
            Status = "All experts have reported. Review the applied fixes, then run the review again to confirm consistency.";
            return;
        }

        MoveTo(StepIndex + 1);
        await RunCurrentStepAsync();
    }

    [RelayCommand]
    private void Back()
    {
        if (IsBusy || StepIndex == 0)
        {
            return;
        }

        MoveTo(StepIndex - 1);
        RefreshStale();
        Status = CurrentStep is { WasRun: true }
            ? $"Loaded: {CurrentStep.Label}. {CurrentStep.Findings.Count} finding(s)."
            : string.Empty;
    }

    [RelayCommand]
    private async Task SkipAsync()
    {
        if (IsBusy || CurrentStep is null)
        {
            return;
        }

        CurrentStep.IsSkipped = true;
        CurrentStep.Status = "Skipped.";
        await NextAsync();
    }

    [RelayCommand]
    private async Task RestartAsync()
    {
        if (IsBusy)
        {
            return;
        }

        foreach (var step in Steps)
        {
            step.Findings.Clear();
            step.WasRun = false;
            step.IsStale = false;
            step.IsSkipped = false;
            step.Signature = null;
            step.Status = "Not run yet.";
        }

        IsFinished = false;
        MoveTo(0);
        await RunCurrentStepAsync();
    }

    private void MoveTo(int index)
    {
        StepIndex = index;
        CurrentStep = Steps[index];
        IsFinished = false;
        Status = string.Empty;
    }

    private async Task RunCurrentStepAsync()
    {
        if (IsBusy || CurrentStep is null || IsFinished)
        {
            return;
        }

        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        var step = CurrentStep;

        IsBusy = true;
        step.IsSkipped = false;
        step.IsStale = false;
        step.Findings.Clear();
        SelectedFinding = null;
        step.Status = "Reviewing…";
        Status = $"Reviewing: {step.Label}…";

        var progress = new Progress<GenerationProgress>(report => step.Status = $"{report.Stage}…");

        try
        {
            var findings = await _run(step.Check, Brief, progress, token);
            var canFixWithAi = _host.FixTargets().Count > 0;
            foreach (var finding in findings)
            {
                var viewModel = new ReviewFindingViewModel(finding, _host, canFixWithAi);
                viewModel.Applied += OnFindingApplied;
                viewModel.PropertyChanged += OnFindingPropertyChanged;
                step.Findings.Add(viewModel);
            }

            SelectedFinding = step.Findings.FirstOrDefault();

            foreach (var finding in step.Findings.Where(IsDeterministic))
            {
                await finding.PrepareAsync(token);
            }

            step.WasRun = true;
            step.Signature = _host.ReviewSignature();
            step.Status = findings.Count == 0 ? "No issues found." : $"{findings.Count} finding(s).";
            Status = $"{step.Label}: {step.Status}";
        }
        catch (OperationCanceledException)
        {
            step.Status = "Stopped.";
            Status = $"{step.Label}: stopped.";
        }
        catch (LlmException exception)
        {
            step.Status = $"Failed ({exception.Kind}): {exception.Message}";
            Status = step.Status;
        }
        catch (Exception exception)
        {
            step.Status = $"Failed: {exception.Message}";
            Status = step.Status;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static bool IsDeterministic(ReviewFindingViewModel finding) =>
        finding.Action is ReviewFindingAction.Fix or ReviewFindingAction.CreateEntry;

    private void OnFindingApplied(object? sender, EventArgs e)
    {
        if (sender is ReviewFindingViewModel finding)
        {
            Status = $"Applied: {finding.Title}";
        }

        RefreshStale();
        OnPropertyChanged(nameof(ApplySelectedLabel));
        OnPropertyChanged(nameof(CanApplySelected));
    }

    private void OnFindingPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ReviewFindingViewModel.IsSelected))
        {
            OnPropertyChanged(nameof(ApplySelectedLabel));
            OnPropertyChanged(nameof(CanApplySelected));
        }
    }

    private void RefreshStale()
    {
        var current = _host.ReviewSignature();
        foreach (var step in Steps.Where(candidate => candidate.WasRun))
        {
            step.IsStale = step.Signature != current;
        }
    }
}
