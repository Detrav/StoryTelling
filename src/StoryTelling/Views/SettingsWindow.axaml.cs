using Avalonia.Controls;
using Avalonia.Interactivity;
using StoryTelling.Application.Settings;
using StoryTelling.ViewModels;

namespace StoryTelling.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow() => InitializeComponent();

    private void OnApplyClick(object? sender, RoutedEventArgs e) => Close(true);

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);

    private void OnRemoveLanguageClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: LanguageData language } && DataContext is SettingsWindowViewModel viewModel)
        {
            viewModel.RemoveLanguageCommand.Execute(language);
        }
    }
}
