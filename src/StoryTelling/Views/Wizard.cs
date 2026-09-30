using Avalonia;
using Avalonia.Controls;
using StoryTelling.ViewModels;

namespace StoryTelling.Views;

internal static class Wizard
{
    public static async Task<string?> RunAsync(Visual owner, string label)
    {
        if (TopLevel.GetTopLevel(owner) is not Window window)
        {
            return null;
        }

        var wizard = new AiWizardWindow { DataContext = new AiWizardViewModel(label) };
        return await wizard.ShowDialog<string?>(window);
    }
}
