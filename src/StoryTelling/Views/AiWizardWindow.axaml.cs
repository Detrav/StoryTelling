using Avalonia.Controls;
using Avalonia.Interactivity;
using StoryTelling.ViewModels;

namespace StoryTelling.Views;

public partial class AiWizardWindow : Window
{
    public AiWizardWindow()
    {
        InitializeComponent();
        Closed += (_, _) => (DataContext as AiWizardViewModel)?.Cancel();
    }

    private void OnApplyClick(object? sender, RoutedEventArgs e) =>
        Close((DataContext as AiWizardViewModel)?.Result);

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close((IReadOnlyDictionary<string, string>?)null);
}
