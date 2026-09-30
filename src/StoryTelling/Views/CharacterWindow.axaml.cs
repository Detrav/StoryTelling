using Avalonia.Controls;
using Avalonia.Interactivity;
using StoryTelling.Application.Generation;
using StoryTelling.ViewModels;

namespace StoryTelling.Views;

public partial class CharacterWindow : Window
{
    public CharacterWindow() => InitializeComponent();

    private void OnOkClick(object? sender, RoutedEventArgs e) => Close(true);

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);

    private async void OnGenerateClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not CharacterEditorViewModel character || character.GenerateOptions is not { } generate)
        {
            return;
        }

        var wizard = new AiWizardWindow
        {
            DataContext = new AiWizardViewModel(
                GenerationTargets.Label(GenerationTarget.Character),
                GenerationTarget.Character,
                (brief, options, session, progress, cancellationToken) => generate(brief, options, session, progress, cancellationToken)),
        };

        var result = await wizard.ShowDialog<IReadOnlyDictionary<string, string>?>(this);
        if (result is { Count: > 0 })
        {
            character.ApplyFields(result);
        }
    }
}
