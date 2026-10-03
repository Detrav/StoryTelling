using Avalonia.Controls;
using Avalonia.Interactivity;
using StoryTelling.ViewModels;

namespace StoryTelling.Views;

public partial class ProjectReviewWindow : Window
{
    public ProjectReviewWindow()
    {
        InitializeComponent();
        Closed += (_, _) => (DataContext as ProjectReviewViewModel)?.Cancel();
        Opened += async (_, _) =>
        {
            if (DataContext is ProjectReviewViewModel viewModel)
            {
                await viewModel.StartAsync();
            }
        };
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
