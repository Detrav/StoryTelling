using Avalonia.Controls;
using Avalonia.Interactivity;
using StoryTelling.ViewModels;

namespace StoryTelling.Views;

public partial class ChapterSummaryView : UserControl
{
    public ChapterSummaryView() => InitializeComponent();

    private async void OnGenerateClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ChapterSummaryViewModel viewModel)
        {
            return;
        }

        var result = await Wizard.RunAsync(this, "Chapter recap");
        if (!string.IsNullOrWhiteSpace(result))
        {
            viewModel.Recap = result;
        }
    }
}
