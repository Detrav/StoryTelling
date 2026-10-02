using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using StoryTelling.Application.Generation;
using StoryTelling.ViewModels;

namespace StoryTelling.Views;

public partial class ChapterSettingsView : UserControl
{
    public ChapterSettingsView() => InitializeComponent();

    private async void OnGenerateClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ChapterSettingsViewModel viewModel || viewModel.GenerateOptions is not { } generate)
        {
            return;
        }

        var wizard = new AiWizardWindow
        {
            DataContext = new AiWizardViewModel(
                GenerationTargets.Label(GenerationTarget.ChapterSettings),
                GenerationTarget.ChapterSettings,
                (brief, options, session, progress, cancellationToken) => generate(brief, options, session, progress, cancellationToken)),
        };

        var result = await wizard.ShowDialog<IReadOnlyDictionary<string, string>?>(GetWindow());
        if (result is { Count: > 0 })
        {
            viewModel.ApplyGenerated(result);
        }
    }

    private void OnApplyDirectionSuggestionClick(object? sender, RoutedEventArgs e) => (DataContext as ChapterSettingsViewModel)?.ApplyDirectionSuggestion();

    private void OnDismissDirectionSuggestionClick(object? sender, RoutedEventArgs e) => (DataContext as ChapterSettingsViewModel)?.DismissDirectionSuggestion();

    private void OnCommit(object? sender, FocusChangedEventArgs e) => (DataContext as ChapterSettingsViewModel)?.Commit();

    private Window GetWindow() => (Window)TopLevel.GetTopLevel(this)!;
}
