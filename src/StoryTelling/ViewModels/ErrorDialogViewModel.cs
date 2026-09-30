namespace StoryTelling.ViewModels;

public partial class ErrorDialogViewModel : ViewModelBase
{
    public ErrorDialogViewModel(string title, string message, string? details)
    {
        Title = title;
        Message = message;
        Details = details ?? string.Empty;
    }

    public string Title { get; }

    public string Message { get; }

    public string Details { get; }

    public bool HasDetails => !string.IsNullOrWhiteSpace(Details);
}
