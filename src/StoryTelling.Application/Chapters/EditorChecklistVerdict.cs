namespace StoryTelling.Application.Chapters;

public sealed record EditorCheckResult(string Id, bool Ok, string Reason);

public sealed record EditorChecklistVerdict(IReadOnlyList<EditorCheckResult> Results)
{
    public static EditorChecklistVerdict Empty { get; } = new([]);

    public IReadOnlyList<EditorCheckResult> Failures =>
        [.. Results.Where(result => !result.Ok)];
}
