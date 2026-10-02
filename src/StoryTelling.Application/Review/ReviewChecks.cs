using StoryTelling.Domain;

namespace StoryTelling.Application.Review;

public static class ReviewChecks
{
    public static ReviewCheck Numbers { get; } = new(
        "numbers",
        "Numbers & timeline",
        ReviewScope.Project,
        "Audit the story bible's numbers: every stated age, date, year, duration and rank, across all entries.",
        [
            "Ages and dates: estimate a birth year for every stated age/year (including ages embedded in Event entries about a relative) and compare across entries; check that the ages of relatives are mutually possible (parent vs child, siblings, spouses). Flag impossible or inconsistent numbers.",
            "Timeline: event years vs \"N years ago\" vs ages vs tenure (time spent in a job or role), including whether an event can fit before or after another.",
            "Roles and titles that drift between entries.",
        ],
        Reconcile: true,
        IncludeInitialState: true);

    public static ReviewCheck Facts { get; } = new(
        "facts",
        "Facts & metadata",
        ReviewScope.Project,
        "Audit the story bible's facts and metadata: tags, kinds, roles and the same fact stated in several entries.",
        [
            "Tags vs content: kind, role, status, age, gender, rank or affiliation that disagrees with the entry body. Check every tag against the body, for every entry.",
            "Conflicting facts: the same object, event or location described differently in two or more entries (for example where contraband was hidden, or the name of a group).",
            "Misplaced content: details that belong to a different entry (for example a character trait inside a Place entry).",
        ]);

    public static ReviewCheck Entities { get; } = new(
        "entities",
        "Entities & references",
        ReviewScope.Project,
        "Audit the story bible's entities: named people, places, groups and objects against what exists.",
        [
            "Dangling references: a person, place, group or object that is referenced but has no entry anywhere.",
            "Missing entries: a named entity that is central to the story but has no knowledge entry.",
            "Scope ambiguity: an entity that is used both as one character's alias and as a separate group (or similar).",
        ]);

    public static ReviewCheck Setting { get; } = new(
        "setting",
        "Setting & geography",
        ReviewScope.Project,
        "Audit the story bible's world: the setting, geography, era and technology against the places and events.",
        [
            "Setting vs entries: the world's geography, era and technology vs the places and events (for example a fictional setting vs real-world place names).",
            "Internal world consistency: the places, distances and locations against each other.",
        ],
        Kinds: [KnowledgeKind.Place, KnowledgeKind.Event, KnowledgeKind.Rule, KnowledgeKind.Background],
        IncludeWorld: true,
        MaxChars: 6000);

    public static ReviewCheck Identity { get; } = new(
        "identity",
        "Identity & names",
        ReviewScope.Project,
        "Audit the story bible's characters: names, identities and descriptions against each other.",
        [
            "Identity mix-ups: an entry whose text names or describes a different entry's character, or an entry that reuses another entry's wording.",
            "Name clashes: characters that share a surname or first name without a stated relationship.",
            "Tag vs content for characters: protagonist/role tags that do not match the character's description.",
        ],
        Kinds: [KnowledgeKind.Character]);

    public static IReadOnlyList<ReviewCheck> All { get; } = [Numbers, Facts, Entities, Setting, Identity];

    public static IReadOnlyList<ReviewCheck> ForScope(ReviewScope scope) =>
        [.. All.Where(check => check.Scope == scope)];

    public static ReviewCheck? Find(string id) =>
        All.FirstOrDefault(check => string.Equals(check.Id, id, StringComparison.OrdinalIgnoreCase));
}
