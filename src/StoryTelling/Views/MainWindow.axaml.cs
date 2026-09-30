using Avalonia.Controls;
using Avalonia.Interactivity;
using StoryTelling.ViewModels;

namespace StoryTelling.Views;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    private async void OnProjectSetupClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel main)
        {
            return;
        }

        var viewModel = main.CreateSetupViewModel();
        var window = new SetupWindow { DataContext = viewModel };
        var applied = await window.ShowDialog<bool>(this);
        if (applied)
        {
            main.ApplySetup(viewModel);
        }
    }

    private void OnSettingsClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel main)
        {
            return;
        }

        var window = new SettingsWindow { DataContext = new SettingsWindowViewModel(main.Languages) };
        _ = window.ShowDialog(this);
    }

    private void OnAboutClick(object? sender, RoutedEventArgs e)
    {
        _ = new AboutWindow().ShowDialog(this);
    }

    private void OnExitClick(object? sender, RoutedEventArgs e) => Close();
}
