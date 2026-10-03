namespace StoryTelling.Application.Chapters;

public static class EditorChecks
{
    public static EditorCheck WorldCanon { get; } = new(
        "world-canon",
        "World canon",
        "Does the prose contradict the world's fixed rules — physics, astronomy, geography, climate, toponyms, or a real-world name the world forbids?",
        "Remove the contradiction while keeping the same scene and events; align the prose with the world's fixed rules.");

    public static EditorCheck Status { get; } = new(
        "status",
        "Entity status",
        "Is every entity's alive/dead/status exactly as the knowledge base states it, with no death or revival the base does not support?",
        "Rewrite the status mentions so they match the knowledge base; the base is authoritative.");

    public static EditorCheck FactsNumbers { get; } = new(
        "facts-numbers",
        "Facts & numbers",
        "Are ages, dates, durations and distances consistent with the knowledge base and with each other?",
        "Bring the numbers and dates to the canonical values from the knowledge base.");

    public static EditorCheck Relations { get; } = new(
        "relations",
        "Relations & roles",
        "Do kinship and official relations (who is whose parent, child, spouse, captain, subordinate) match the knowledge base?",
        "Correct the relations and roles to match the knowledge base.");

    public static EditorCheck Continuity { get; } = new(
        "continuity",
        "Continuity",
        "Does the chapter restart the story, revive a resolved thread, ignore an open one, or contradict the recap or the world state before this chapter?",
        "Tie the chapter to the current state without restarting; keep open threads open and resolved threads closed.");

    public static EditorCheck PovTense { get; } = new(
        "pov-tense",
        "POV & tense",
        "Do the point of view and the tense match the narrative frame declared in the world?",
        "Correct the point of view and tense drift to match the declared frame.");

    public static IReadOnlyList<EditorCheck> All { get; } = [WorldCanon, Status, FactsNumbers, Relations, Continuity, PovTense];

    public static EditorCheck? Find(string id) =>
        All.FirstOrDefault(check => string.Equals(check.Id, id, StringComparison.OrdinalIgnoreCase));
}
