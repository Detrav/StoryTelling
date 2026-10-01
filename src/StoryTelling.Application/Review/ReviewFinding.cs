namespace StoryTelling.Application.Review;

public sealed record ReviewFinding(
    ReviewSeverity Severity,
    ReviewArea Area,
    string Title,
    string Detail,
    string? Suggestion,
    ReviewFix? Fix = null,
    string? Reference = null);
