using StoryTelling.Domain;

namespace StoryTelling.Application.Chapters;

public static class FinaleGuard
{
    public const string Contract =
        "FINAL-CHAPTER CONTRACT: this is the last chapter. Resolve every open thread in it and end on a "
        + "stable, closed final note. Do not end with a cliffhanger and do not promise that the story, "
        + "mystery or conflict continues.";

    public const string StrictContract =
        "FINAL-CHAPTER CONTRACT (STRICT): the previous attempt still left the story unresolved or ended on "
        + "a hook. Rewrite it so the central mystery or conflict reaches a definitive closure and the final "
        + "paragraph offers no continuation at all. No open thread may remain.";

    private static readonly string[] _cliffhangerMarkers =
    [
        "to be continued",
        "will be continued",
        "will not last forever",
        "will not stay silent",
        "it will answer again",
        "will answer again",
        "the story continues",
        "not the end",
        "not over yet",
        "will return",
        "another chapter",
        "remains unresolved",
        "leaves the door open",
        "sets up a sequel",
        "sets up future",
    ];

    public static bool IsUnresolved(ChapterResult result) =>
        result.KnowledgeChanges.Any(change => change.Kind == KnowledgeKind.Thread && change.Status == KnowledgeStatus.Open)
        || HasCliffhanger(result.Text)
        || HasCliffhanger(result.WorldState?.Situation)
        || HasCliffhanger(result.WorldState?.TimeAndPlace)
        || HasCliffhanger(result.Logline);

    public static bool HasCliffhanger(string? text) =>
        !string.IsNullOrWhiteSpace(text)
        && _cliffhangerMarkers.Any(marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase));
}
