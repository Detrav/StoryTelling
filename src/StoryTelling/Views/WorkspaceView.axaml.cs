using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using StoryTelling.ViewModels;

namespace StoryTelling.Views;

public partial class WorkspaceView : UserControl
{
    public WorkspaceView() => InitializeComponent();

    private void OnDeleteChapterClick(object? sender, RoutedEventArgs e) => Execute(vm => vm.DeleteChapterCommand.Execute(Chapter(sender)));

    private void OnMoveChapterUpClick(object? sender, RoutedEventArgs e) => Execute(vm => vm.MoveChapterUpCommand.Execute(Chapter(sender)));

    private void OnMoveChapterDownClick(object? sender, RoutedEventArgs e) => Execute(vm => vm.MoveChapterDownCommand.Execute(Chapter(sender)));

    private async void OnPlanChaptersClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not WorkspaceViewModel workspace || TopLevel.GetTopLevel(this) is not Window window)
        {
            return;
        }

        var viewModel = new ChapterPlanViewModel(
            (count, brief, session, progress, cancellationToken) => workspace.PlanChaptersAsync(count, brief, session, progress, cancellationToken));

        var dialog = new ChapterPlanWindow { DataContext = viewModel };
        if (!await dialog.ShowDialog<bool>(window) || viewModel.Chapters.Count == 0)
        {
            return;
        }

        if (workspace.HasWrittenContent
            && !await ConfirmDialog.ShowAsync(window, "Replace chapters", "This replaces all chapters and deletes the written text. Continue?"))
        {
            return;
        }

        workspace.ApplyChapterPlan([.. viewModel.Chapters.Select(chapter => (chapter.Title, chapter.Direction))]);
    }

    private void Execute(Action<WorkspaceViewModel> action)
    {
        if (DataContext is WorkspaceViewModel workspace)
        {
            action(workspace);
        }
    }

    private static ChapterViewModel? Chapter(object? sender) => (sender as Control)?.DataContext as ChapterViewModel;
}
