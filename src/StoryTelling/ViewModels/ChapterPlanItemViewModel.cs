namespace StoryTelling.ViewModels;

public sealed class ChapterPlanItemViewModel
{
    public ChapterPlanItemViewModel(int number, string title, string direction)
    {
        Number = number;
        Title = title;
        Direction = direction;
    }

    public int Number { get; }

    public string Title { get; }

    public string Direction { get; }

    public string Header => $"{Number}. {Title}";
}
