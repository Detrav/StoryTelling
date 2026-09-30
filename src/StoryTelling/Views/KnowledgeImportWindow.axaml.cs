using Avalonia.Controls;
using Avalonia.Interactivity;
using StoryTelling.ViewModels;

namespace StoryTelling.Views;

public partial class KnowledgeImportWindow : Window
{
    public KnowledgeImportWindow()
    {
        InitializeComponent();
        Closed += (_, _) => (DataContext as KnowledgeImportViewModel)?.Cancel();
    }

    private void OnApplyClick(object? sender, RoutedEventArgs e) =>
        Close((DataContext as KnowledgeImportViewModel)?.Result);

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);
}
