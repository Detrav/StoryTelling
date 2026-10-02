using StoryTelling.Application.Review;

namespace StoryTelling.Application.Chapters;

public sealed record ContinuityIssue(
    ReviewSeverity Severity,
    string Detail,
    string Reference);
