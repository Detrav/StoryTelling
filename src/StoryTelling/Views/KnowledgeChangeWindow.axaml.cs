using Avalonia.Controls;
using Avalonia.Interactivity;

namespace StoryTelling.Views;

public partial class KnowledgeChangeWindow : Window
{
    public KnowledgeChangeWindow() => InitializeComponent();

    private void OnOkClick(object? sender, RoutedEventArgs e) => Close(true);

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);
}
