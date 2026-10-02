using System.Collections.ObjectModel;
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
    private int _stepIndex;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isFinished;

    [ObservableProperty]
    private string _brief = string.Empty;

    [ObservableProperty]
    private string _status = string.Empty;

    public bool HasSteps => Steps.Count > 0;

    public bool HasPrevious => StepIndex > 0 && !IsFinished;

    public bool ShowFindings => !IsFinished && CurrentStep is not null;

    public string ProgressLabel => IsFinished ? "Done" : $"Step {StepIndex + 1} of {Steps.Count}";

    public string StepTitle => IsFinished ? "Summary" : CurrentStep?.Label ?? string.Empty;

    partial void OnCurrentStepChanged(ReviewStepViewModel? value)
    {
        OnPropertyChanged(nameof(StepTitle));
        OnPropertyChanged(nameof(ShowFindings));
    }

    partial void OnStepIndexChanged(int value)
    {
        OnPropertyChanged(nameof(ProgressLabel));
        OnPropertyChanged(nameof(HasPrevious));
    }

    partial void OnIsFinishedChanged(bool value)
    {
        OnPropertyChanged(nameof(ProgressLabel));
        OnPropertyChanged(nameof(StepTitle));
        OnPropertyChanged(nameof(ShowFindings));
        OnPropertyChanged(nameof(HasPrevious));
    }

    public Task StartAsync() => RunCurrentStepAsync();

    public IReadOnlyList<ReviewChange> PreviewFix(ReviewFix fix) => _host.PreviewFix(fix);

    public void ApplyFix(ReviewFindingViewModel finding, ReviewFix fix)
    {
        _host.ApplyFix(fix, $"Fix: {finding.Title}");
        finding.IsFixed = true;
        Status = $"Fixed: {finding.Title}";
        RefreshStale();
    }

    public void AddEntry(StoryTelling.Domain.KnowledgeEntry entry, string label)
    {
        _host.AddEntry(entry, label);
        Status = label;
        RefreshStale();
    }

    public AiWizardViewModel.GenerateOptions Generate(GenerationTarget target) =>
        (brief, options, session, progress, cancellationToken) =>
            _host.GenerateAsync(target, brief, options, session, progress, cancellationToken);

    public IReadOnlyList<ReviewFixTarget> FixTargets() => _host.FixTargets();

    public void Cancel() => _cts?.Cancel();

    [RelayCommand]
    private Task RunStep() => RunCurrentStepAsync();

    [RelayCommand]
    private void Stop() => Cancel();

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
        step.Status = "Reviewing…";
        Status = $"Reviewing: {step.Label}…";

        var progress = new Progress<GenerationProgress>(report => step.Status = $"{report.Stage}…");

        try
        {
            var findings = await _run(step.Check, Brief, progress, token);
            var canFixWithAi = _host.FixTargets().Count > 0;
            foreach (var finding in findings)
            {
                step.Findings.Add(new ReviewFindingViewModel(finding, _host.SingleReference, canFixWithAi));
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

    private void RefreshStale()
    {
        var current = _host.ReviewSignature();
        foreach (var step in Steps.Where(candidate => candidate.WasRun))
        {
            step.IsStale = step.Signature != current;
        }
    }
}
