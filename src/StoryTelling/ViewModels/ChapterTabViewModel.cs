namespace StoryTelling.ViewModels;

public sealed class ChapterTabViewModel
{
    public ChapterTabViewModel(string header, object content)
    {
        Header = header;
        Content = content;
    }

    public string Header { get; }

    public object Content { get; }
}
