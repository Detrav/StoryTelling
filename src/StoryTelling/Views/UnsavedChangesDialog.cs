using Avalonia.Controls;
using StoryTelling.ViewModels;

namespace StoryTelling.Views;

internal static class UnsavedChangesDialog
{
    public static Task<UnsavedChangesChoice> ShowAsync(Window owner, string projectName)
    {
        var window = new UnsavedChangesWindow { DataContext = new UnsavedChangesViewModel(projectName) };
        return window.ShowDialog<UnsavedChangesChoice>(owner);
    }
}
