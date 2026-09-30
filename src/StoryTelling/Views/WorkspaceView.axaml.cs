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

    private void Execute(Action<WorkspaceViewModel> action)
    {
        if (DataContext is WorkspaceViewModel workspace)
        {
            action(workspace);
        }
    }

    private static MockChapter? Chapter(object? sender) => (sender as Control)?.DataContext as MockChapter;
}
