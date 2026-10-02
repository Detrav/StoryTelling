using Avalonia.Controls;
using Avalonia.Interactivity;
using StoryTelling.Application.Generation;
using StoryTelling.Application.Review;
using StoryTelling.Domain;
using StoryTelling.ViewModels;

namespace StoryTelling.Views;

public partial class ProjectReviewWindow : Window
{
    public ProjectReviewWindow()
    {
        InitializeComponent();
        Closed += (_, _) => (DataContext as ProjectReviewViewModel)?.Cancel();
        Opened += async (_, _) =>
        {
            if (DataContext is ProjectReviewViewModel viewModel)
            {
                await viewModel.StartAsync();
            }
        };
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

    private async void OnCreateEntryClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ProjectReviewViewModel viewModel)
        {
            return;
        }

        if (sender is not Button { Tag: ReviewFindingViewModel finding })
        {
            return;
        }

        var entry = new KnowledgeEntryEditorViewModel
        {
            Kind = GuessKind(finding),
            Title = GuessTitle(finding),
            Content = finding.Detail,
        };

        entry.GenerateOptions = (brief, options, session, progress, cancellationToken) =>
            viewModel.Generate(GenerationTarget.Knowledge)(brief, options, session, progress, cancellationToken);
        var window = new KnowledgeEntryWindow { DataContext = entry };
        if (await window.ShowDialog<bool>(this))
        {
            viewModel.AddEntry(entry.ToEntry(), $"Create: {entry.Title}");
        }
    }

    private static string GuessTitle(ReviewFindingViewModel finding)
    {
        var title = finding.Finding.Reference;
        if (!string.IsNullOrWhiteSpace(title))
        {
            return title.Trim();
        }

        var cleaned = finding.Title
            .Replace("Missing central character entry for", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("Missing entry for", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("Dangling reference to", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("Missing entry:", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim(' ', ':', '-', '.');
        return cleaned.Length > 0 ? cleaned : finding.Title;
    }

    private static KnowledgeKind GuessKind(ReviewFindingViewModel finding)
    {
        var text = $"{finding.Title} {finding.Detail}".ToLowerInvariant();
        if (text.Contains("character") || text.Contains("captain") || text.Contains("surname") || text.Contains("protagonist"))
        {
            return KnowledgeKind.Character;
        }

        if (text.Contains("place") || text.Contains("location") || text.Contains("district"))
        {
            return KnowledgeKind.Place;
        }

        if (text.Contains("group") || text.Contains("syndicate") || text.Contains("organization") || text.Contains("faction"))
        {
            return KnowledgeKind.Faction;
        }

        return KnowledgeKind.Background;
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
