using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using StoryTelling.ViewModels;

namespace StoryTelling.Views;

public partial class ChapterSummaryView : UserControl
{
    public ChapterSummaryView() => InitializeComponent();

    private async void OnAddChangeClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ChapterSummaryViewModel viewModel)
        {
            return;
        }

        var draft = new KnowledgeChangeEditorViewModel();
        var window = new KnowledgeChangeWindow { DataContext = draft };
        if (await window.ShowDialog<bool>(GetWindow()))
        {
            viewModel.AddChange(draft);
        }
    }

    private async void OnEditChangeClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ChapterSummaryViewModel viewModel || viewModel.SelectedChange is not { } selected)
        {
            return;
        }

        var draft = selected.Clone();
        var window = new KnowledgeChangeWindow { DataContext = draft };
        if (await window.ShowDialog<bool>(GetWindow()))
        {
            viewModel.ApplyChangeEdit(selected, draft);
        }
    }

    private async void OnDeleteChangeClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ChapterSummaryViewModel viewModel || viewModel.SelectedChange is not { } selected)
        {
            return;
        }

        var title = string.IsNullOrWhiteSpace(selected.Title) ? "this change" : selected.Title;
        if (await ConfirmDialog.ShowAsync(GetWindow(), "Delete change", $"Remove \"{title}\" from this chapter's knowledge?"))
        {
            viewModel.RemoveChange(selected);
        }
    }

    private void OnCommit(object? sender, FocusChangedEventArgs e) => (DataContext as ChapterSummaryViewModel)?.Commit();

    private Window GetWindow() => (Window)TopLevel.GetTopLevel(this)!;
}
