using Avalonia.Controls;
using Avalonia.Interactivity;
using StoryTelling.ViewModels;

namespace StoryTelling.Views;

public partial class ChapterPlanWindow : Window
{
    public ChapterPlanWindow()
    {
        InitializeComponent();
        Closing += (_, _) => (DataContext as ChapterPlanViewModel)?.Cancel();
    }

    private void OnApplyClick(object? sender, RoutedEventArgs e) => Close(true);

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);
}
