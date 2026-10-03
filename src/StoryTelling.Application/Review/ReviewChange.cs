namespace StoryTelling.Application.Review;

public sealed record ReviewChange(ReviewEdit Edit, string Label, string OldValue, string NewValue);
