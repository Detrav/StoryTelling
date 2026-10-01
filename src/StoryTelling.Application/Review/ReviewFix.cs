namespace StoryTelling.Application.Review;

public sealed record ReviewFix(IReadOnlyList<ReviewEdit> Edits)
{
    public bool IsEmpty => Edits.Count == 0;
}
