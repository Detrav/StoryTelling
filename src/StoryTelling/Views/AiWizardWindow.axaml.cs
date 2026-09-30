using Avalonia.Controls;
using Avalonia.Interactivity;
using StoryTelling.ViewModels;

namespace StoryTelling.Views;

public partial class AiWizardWindow : Window
{
    public AiWizardWindow() => InitializeComponent();

    private void OnApplyClick(object? sender, RoutedEventArgs e) =>
        Close((DataContext as AiWizardViewModel)?.Result);

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close((object?)null);
}
