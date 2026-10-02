using StoryTelling.Application.Review;

namespace StoryTelling.Application.Chapters;

public sealed record EditorIssue(
    ReviewSeverity Severity,
    string Detail,
    string Reference);

public sealed record EditorVerdict(bool Integrity, IReadOnlyList<EditorIssue> Issues)
{
    public static EditorVerdict Ok => new(true, []);

    public bool HasError => Issues.Any(issue => issue.Severity == ReviewSeverity.Error);
}
