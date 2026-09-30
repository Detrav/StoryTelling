using Avalonia.Controls;
using Avalonia.Input;
using StoryTelling.ViewModels;

namespace StoryTelling.Views;

public partial class ChapterTextView : UserControl
{
    public ChapterTextView() => InitializeComponent();

    private void OnCommit(object? sender, FocusChangedEventArgs e) => (DataContext as ChapterTextViewModel)?.Commit();
}
