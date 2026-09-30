using Avalonia.Controls;
using StoryTelling.ViewModels;

namespace StoryTelling.Views;

internal static class ErrorDialog
{
    public static void Show(Window owner, string title, string message, string? details = null)
    {
        var window = new ErrorWindow { DataContext = new ErrorDialogViewModel(title, message, details) };
        _ = window.ShowDialog(owner);
    }
}
