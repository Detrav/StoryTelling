using Avalonia;
using Avalonia.Controls;
using StoryTelling.ViewModels;

namespace StoryTelling.Views;

internal static class ChapterGenerationRunner
{
    public static async Task RunAsync(Visual owner, WorkspaceViewModel workspace)
    {
        if (TopLevel.GetTopLevel(owner) is not Window window || !workspace.ValidateGeneration())
        {
            return;
        }

        var viewModel = new ChapterGenerationViewModel(workspace.GenerateChapterAsync);
        var dialog = new ChapterGenerationWindow { DataContext = viewModel };
        await dialog.ShowDialog(window);
    }
}
