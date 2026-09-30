namespace StoryTelling.ViewModels;

public sealed class ConfirmDialogViewModel
{
    public ConfirmDialogViewModel(string title, string message)
    {
        Title = title;
        Message = message;
    }

    public string Title { get; }

    public string Message { get; }
}
