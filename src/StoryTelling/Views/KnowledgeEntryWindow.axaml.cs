using Avalonia.Controls;
using Avalonia.Interactivity;
using StoryTelling.Application.Generation;
using StoryTelling.ViewModels;

namespace StoryTelling.Views;

public partial class KnowledgeEntryWindow : Window
{
    public KnowledgeEntryWindow() => InitializeComponent();

    private void OnOkClick(object? sender, RoutedEventArgs e) => Close(true);

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);

    private async void OnGenerateClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not KnowledgeEntryEditorViewModel entry || entry.GenerateOptions is not { } generate)
        {
            return;
        }

        var wizard = new AiWizardWindow
        {
            DataContext = new AiWizardViewModel(
                GenerationTargets.Label(GenerationTarget.Knowledge),
                GenerationTarget.Knowledge,
                (brief, options, session, progress, cancellationToken) => generate(brief, options, session, progress, cancellationToken)),
        };

        var result = await wizard.ShowDialog<IReadOnlyDictionary<string, string>?>(this);
        if (result is { Count: > 0 })
        {
            entry.ApplyFields(result);
        }
    }
}
