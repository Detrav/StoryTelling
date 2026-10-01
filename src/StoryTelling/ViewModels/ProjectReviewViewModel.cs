using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StoryTelling.Application.Generation;
using StoryTelling.Application.Llm;
using StoryTelling.Application.Review;

namespace StoryTelling.ViewModels;

public partial class ProjectReviewViewModel : ViewModelBase
{
    public delegate Task<IReadOnlyList<ReviewFinding>> RunReview(string brief, IProgress<GenerationProgress>? progress, CancellationToken cancellationToken);

    private readonly RunReview _run;
    private readonly IReviewFixHost _host;
    private CancellationTokenSource? _cts;

    public ProjectReviewViewModel(RunReview run, IReviewFixHost host)
    {
        _run = run;
        _host = host;
    }

    public ObservableCollection<ReviewFindingViewModel> Findings { get; } = [];

    [ObservableProperty]
    private string _brief = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _status = "Press \"Run review\" to check the project for inconsistencies.";

    public IReadOnlyList<ReviewChange> PreviewFix(ReviewFix fix) => _host.PreviewFix(fix);

    public void ApplyFix(ReviewFindingViewModel finding, ReviewFix fix)
    {
        _host.ApplyFix(fix, $"Fix: {finding.Title}");
        finding.IsFixed = true;
        Status = $"Fixed: {finding.Title}";
    }

    public AiWizardViewModel.GenerateOptions Generate(GenerationTarget target) =>
        (brief, options, session, progress, cancellationToken) =>
            _host.GenerateAsync(target, brief, options, session, progress, cancellationToken);

    public IReadOnlyList<ReviewFixTarget> FixTargets() => _host.FixTargets();

    [RelayCommand]
    private Task Run() => RunAsync();

    [RelayCommand]
    private void Stop() => Cancel();

    public void Cancel() => _cts?.Cancel();

    private async Task RunAsync()
    {
        if (IsBusy)
        {
            return;
        }

        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        IsBusy = true;
        Status = "Reviewing…";
        Findings.Clear();

        var progress = new Progress<GenerationProgress>(report =>
            Status = report.ToolCalls > 0 ? $"{report.Stage}… ({report.ToolCalls} tool calls)" : $"{report.Stage}…");

        try
        {
            var findings = await _run(Brief, progress, token);
            var canFixWithAi = _host.FixTargets().Count > 0;
            foreach (var finding in findings)
            {
                Findings.Add(new ReviewFindingViewModel(finding, _host.SingleReference, canFixWithAi));
            }

            Status = findings.Count == 0 ? "No issues found." : $"{findings.Count} findings.";
        }
        catch (OperationCanceledException)
        {
            Status = "Stopped.";
        }
        catch (LlmException exception)
        {
            Status = $"Failed ({exception.Kind}): {exception.Message}";
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
}
