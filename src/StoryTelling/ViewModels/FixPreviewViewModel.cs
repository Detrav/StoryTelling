using StoryTelling.Application.Review;

namespace StoryTelling.ViewModels;

public sealed class FixPreviewViewModel
{
    public FixPreviewViewModel(string title, IReadOnlyList<ReviewChange> changes)
    {
        Title = title;
        Changes = changes;
    }

    public string Title { get; }

    public IReadOnlyList<ReviewChange> Changes { get; }
}
