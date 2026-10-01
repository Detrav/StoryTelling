using Avalonia;
using Avalonia.Controls;
using StoryTelling.Application.Generation;
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

        var wizard = new AiWizardWindow
        {
            DataContext = new AiWizardViewModel(
                label,
                GenerationTarget.World,
                (_, _, _, _, _) => Task.FromResult<IReadOnlyList<GenerationOption>>([]),
                editableField: "Text"),
        };

        var result = await wizard.ShowDialog<IReadOnlyDictionary<string, string>?>(window);
        return result?.Values.FirstOrDefault();
    }
}
