using Avalonia.Controls;
using Avalonia.Interactivity;

namespace StoryTelling.Views;

public partial class FixPreviewWindow : Window
{
    public FixPreviewWindow() => InitializeComponent();

    private void OnApplyClick(object? sender, RoutedEventArgs e) => Close(true);

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);
}
