using Avalonia.Controls;
using Avalonia.Interactivity;
using StoryTelling.ViewModels;

namespace StoryTelling.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow() => InitializeComponent();

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void OnRemoveLanguageClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: LanguageOption option } && DataContext is SettingsWindowViewModel viewModel)
        {
            viewModel.RemoveLanguageCommand.Execute(option);
        }
    }
}
