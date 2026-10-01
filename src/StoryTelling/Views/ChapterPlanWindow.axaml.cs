using Avalonia.Controls;
using Avalonia.Interactivity;

namespace StoryTelling.Views;

public partial class ChapterPlanWindow : Window
{
    public ChapterPlanWindow() => InitializeComponent();

    private void OnApplyClick(object? sender, RoutedEventArgs e) => Close(true);

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);
}
