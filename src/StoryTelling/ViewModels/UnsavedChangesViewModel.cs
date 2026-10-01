namespace StoryTelling.ViewModels;

public sealed class UnsavedChangesViewModel
{
    public UnsavedChangesViewModel(string projectName)
    {
        ProjectName = projectName;
    }

    public string ProjectName { get; }

    public string Title => "Unsaved changes";

    public string Message =>
        $"“{ProjectName}” has unsaved changes. Save before closing?";
}
