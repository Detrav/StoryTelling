using Avalonia.Controls;
using StoryTelling.ViewModels;

namespace StoryTelling.Views;

internal static class ConfirmDialog
{
    public static async Task<bool> ShowAsync(Window owner, string title, string message)
    {
        var window = new ConfirmWindow { DataContext = new ConfirmDialogViewModel(title, message) };
        return await window.ShowDialog<bool>(owner);
    }
}
