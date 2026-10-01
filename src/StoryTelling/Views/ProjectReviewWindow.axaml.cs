using Avalonia.Controls;
using Avalonia.Interactivity;
using StoryTelling.Application.Generation;
using StoryTelling.Application.Review;
using StoryTelling.ViewModels;

namespace StoryTelling.Views;

public partial class ProjectReviewWindow : Window
{
    public ProjectReviewWindow()
    {
        InitializeComponent();
        Closed += (_, _) => (DataContext as ProjectReviewViewModel)?.Cancel();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private async void OnFixClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ProjectReviewViewModel viewModel)
        {
            return;
        }

        if (sender is not Button { Tag: ReviewFindingViewModel finding } || finding.Finding.Fix is not { IsEmpty: false } fix)
        {
            return;
        }

        await PreviewAndApplyAsync(viewModel, finding, fix);
    }

    private async void OnAiFixClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ProjectReviewViewModel viewModel)
        {
            return;
        }

        if (sender is not Button { Tag: ReviewFindingViewModel finding })
        {
            return;
        }

        GenerationTarget target;
        string reference;

        if (finding.AiTarget is { } known)
        {
            target = known;
            reference = finding.AiReference;
        }
        else
        {
            var choices = viewModel.FixTargets();
            if (choices.Count == 0)
            {
                ErrorDialog.Show(this, "Nothing to fix", "There is nothing in the project an AI fix could be applied to yet.");
                return;
            }

            var picker = new FixTargetPickerWindow { DataContext = new FixTargetPickerViewModel(choices) };
            if (await picker.ShowDialog<ReviewFixTarget?>(this) is not { } chosen)
            {
                return;
            }

            target = chosen.Target;
            reference = chosen.Reference;
        }

        await RunAiFixAsync(viewModel, finding, target, reference);
    }

    private async Task RunAiFixAsync(ProjectReviewViewModel viewModel, ReviewFindingViewModel finding, GenerationTarget target, string reference)
    {
        var brief = string.Join("\n\n", new[] { finding.Detail, finding.Suggestion }.Where(text => !string.IsNullOrWhiteSpace(text)));
        var wizard = new AiWizardWindow
        {
            DataContext = new AiWizardViewModel(GenerationTargets.Label(target), target, viewModel.Generate(target), brief),
        };

        var result = await wizard.ShowDialog<IReadOnlyDictionary<string, string>?>(this);
        if (result is not { Count: > 0 })
        {
            return;
        }

        var edits = result
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .Select(pair => new ReviewEdit(target, reference, pair.Key, pair.Value))
            .ToList();

        if (edits.Count > 0)
        {
            await PreviewAndApplyAsync(viewModel, finding, new ReviewFix(edits));
        }
    }

    private async Task PreviewAndApplyAsync(ProjectReviewViewModel viewModel, ReviewFindingViewModel finding, ReviewFix fix)
    {
        var changes = viewModel.PreviewFix(fix);
        if (changes.Count == 0)
        {
            ErrorDialog.Show(
                this,
                "Nothing to apply",
                "The fix could not be resolved against the current setup (the referenced character or entry may be missing).");
            return;
        }

        var preview = new FixPreviewWindow
        {
            DataContext = new FixPreviewViewModel($"Fix: {finding.Title}", changes),
        };

        if (await preview.ShowDialog<bool>(this))
        {
            viewModel.ApplyFix(finding, fix);
        }
    }
}
