using Avalonia.Controls;
using Avalonia.Interactivity;
using StoryTelling.ViewModels;

namespace StoryTelling.Views;

public partial class ChapterSettingsView : UserControl
{
    public ChapterSettingsView() => InitializeComponent();

    private async void OnGenerateClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string field } || DataContext is not ChapterSettingsViewModel viewModel)
        {
            return;
        }

        var result = await Wizard.RunAsync(this, viewModel.LabelFor(field));
        if (!string.IsNullOrWhiteSpace(result))
        {
            viewModel.ApplyGenerated(field, result);
        }
    }
}
