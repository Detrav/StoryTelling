using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using StoryTelling.ViewModels;

namespace StoryTelling.Views;

public partial class ChapterSettingsView : UserControl
{
    public ChapterSettingsView() => InitializeComponent();

    private async void OnGenerateClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ChapterSettingsViewModel viewModel || viewModel.Suggest is not { } suggest)
        {
            return;
        }

        var setup = new ChapterSetupViewModel(
            "Chapter settings",
            isAddMode: false,
            viewModel.Role,
            viewModel.Notes,
            (role, notes, variants, session, progress, cancellationToken) =>
                suggest(role, notes, variants, session, progress, cancellationToken));
        var dialog = new ChapterSetupWindow { DataContext = setup };
        if (await dialog.ShowDialog<bool>(GetWindow()) && setup.SelectedOption is { } option)
        {
            viewModel.ApplyChapterSetup(setup.Role, setup.Notes, option.Title, option.Direction);
        }
    }

    private void OnApplyDirectionSuggestionClick(object? sender, RoutedEventArgs e) => (DataContext as ChapterSettingsViewModel)?.ApplyDirectionSuggestion();

    private void OnDismissDirectionSuggestionClick(object? sender, RoutedEventArgs e) => (DataContext as ChapterSettingsViewModel)?.DismissDirectionSuggestion();

    private void OnCommit(object? sender, FocusChangedEventArgs e) => (DataContext as ChapterSettingsViewModel)?.Commit();

    private Window GetWindow() => (Window)TopLevel.GetTopLevel(this)!;
}
