using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using StoryTelling.Domain;
using StoryTelling.ViewModels;

namespace StoryTelling.Views;

public partial class WorkspaceView : UserControl
{
    public WorkspaceView() => InitializeComponent();

    private void OnDeleteChapterClick(object? sender, RoutedEventArgs e) => Execute(vm => vm.DeleteChapterCommand.Execute(Chapter(sender)));

    private void OnMoveChapterUpClick(object? sender, RoutedEventArgs e) => Execute(vm => vm.MoveChapterUpCommand.Execute(Chapter(sender)));

    private void OnMoveChapterDownClick(object? sender, RoutedEventArgs e) => Execute(vm => vm.MoveChapterDownCommand.Execute(Chapter(sender)));

    private void OnSetStatusDraftClick(object? sender, RoutedEventArgs e) => SetStatus(sender, ChapterStatus.Draft);

    private void OnSetStatusGeneratedClick(object? sender, RoutedEventArgs e) => SetStatus(sender, ChapterStatus.Generated);

    private void OnSetStatusStaleClick(object? sender, RoutedEventArgs e) => SetStatus(sender, ChapterStatus.Stale);

    private void SetStatus(object? sender, ChapterStatus status) =>
        Execute(vm => vm.SetChapterStatus(Chapter(sender), status));

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

    private async void OnCompleteBookClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not WorkspaceViewModel workspace || TopLevel.GetTopLevel(this) is not Window window)
        {
            return;
        }

        var viewModel = new BookCompletionViewModel(workspace.BuildCompletionPlan, workspace.CompleteBookAsync);
        var dialog = new BookCompletionWindow { DataContext = viewModel };
        await dialog.ShowDialog(window);
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
