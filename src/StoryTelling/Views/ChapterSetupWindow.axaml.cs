using Avalonia.Controls;
using Avalonia.Interactivity;
using StoryTelling.ViewModels;

namespace StoryTelling.Views;

public partial class ChapterSetupWindow : Window
{
    public ChapterSetupWindow()
    {
        InitializeComponent();
        Closed += (_, _) => (DataContext as ChapterSetupViewModel)?.Cancel();
    }

    private void OnApplyClick(object? sender, RoutedEventArgs e) => Close(true);

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);
}
