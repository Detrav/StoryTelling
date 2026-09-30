using Avalonia.Controls;
using Avalonia.Interactivity;

namespace StoryTelling.Views;

public partial class KnowledgeEntryWindow : Window
{
    public KnowledgeEntryWindow() => InitializeComponent();

    private void OnOkClick(object? sender, RoutedEventArgs e) => Close(true);

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);
}
