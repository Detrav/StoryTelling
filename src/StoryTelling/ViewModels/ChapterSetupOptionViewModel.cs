namespace StoryTelling.ViewModels;

public sealed class ChapterSetupOptionViewModel
{
    public ChapterSetupOptionViewModel(string title, string direction)
    {
        Title = title;
        Direction = direction;
    }

    public string Title { get; }

    public string Direction { get; }

    public bool HasTitle => Title.Length > 0;
}
