using System.Text.Json.Nodes;

namespace StoryTelling.Application.Generation;

public static class GenerationTargets
{
    private static readonly IReadOnlyDictionary<GenerationTarget, IReadOnlyList<GenerationFieldSpec>> _specs =
        new Dictionary<GenerationTarget, IReadOnlyList<GenerationFieldSpec>>
        {
            [GenerationTarget.World] =
            [
                new("WorldTitle", "title", "World title", "A short, evocative name for the story's world or setting."),
                new("WorldBody", "body", "World description", "A vivid description of the world and setting (2-5 sentences)."),
            ],
            [GenerationTarget.ProjectName] =
            [
                new("ProjectName", "name", "Book name", "A short, evocative title for the book."),
            ],
            [GenerationTarget.Premise] =
            [
                new("Premise", "premise", "Premise", "A compelling story premise (2-4 sentences)."),
            ],
            [GenerationTarget.Frame] =
            [
                new("Genre", "genre", "Genre", "The story's genre, e.g. 'dark fantasy'."),
                new("Tone", "tone", "Tone", "The overall tone and mood, e.g. 'grim, hopeful, lyrical'."),
                new("Style", "style", "Style", "The narrative voice, e.g. 'terse, atmospheric, sensory-heavy'."),
                new("PointOfView", "pointOfView", "Point of view", "e.g. 'third person limited', 'first person'."),
                new("Tense", "tense", "Tense", "e.g. 'past', 'present'."),
                new("Rating", "rating", "Rating", "Content rating, e.g. 'PG-13', 'R'."),
                new("Premise", "premise", "Premise", "A compelling story premise (2-4 sentences)."),
                new("Direction", "direction", "Direction", "The intended overall arc and where the story is heading (2-3 sentences)."),
            ],
            [GenerationTarget.Character] =
            [
                new("Name", "name", "Name", "The character's full name."),
                new("Role", "role", "Role", "Their role in the story (protagonist, antagonist, supporting, ...). Leave empty if unclear."),
                new("Age", "age", "Age", "Their age or age range as a short phrase, e.g. '27' or 'mid-30s'. Do not repeat the role here."),
                new("Description", "description", "Description", "Who they are and how they look (1-2 sentences)."),
                new("Personality", "personality", "Personality", "Their character, temperament and voice (1-2 sentences)."),
                new("Background", "background", "Background", "Their backstory and what shaped them (2-4 sentences)."),
                new("Goals", "goals", "Goals", "What they want and why. Leave empty if unclear."),
                new("Traits", "traits", "Traits", "Three to six very short tags, e.g. 'brave', 'sarcastic', 'loyal'. Never write sentences.", IsList: true),
            ],
            [GenerationTarget.Knowledge] =
            [
                new("Kind", "kind", "Kind", "One of: Note, Place, Item, Event, Faction, Rule, Background."),
                new("Title", "title", "Title", "A short, specific name for the entry."),
                new("Tags", "tags", "Tags", "A few short tags, e.g. 'city', 'port', 'trade'. Never write sentences.", IsList: true),
                new("Content", "content", "Content", "A self-contained description of the entry; keep the important details."),
            ],
            [GenerationTarget.WorldState] =
            [
                new("TimeAndPlace", "timeAndPlace", "Time and place", "When and where the story opens (a short phrase)."),
                new("Description", "description", "Description", "The situation before chapter 1 in free form: what is happening, who is involved, notable facts, tensions and open questions."),
            ],
        };

    public static IReadOnlyList<GenerationFieldSpec> Fields(GenerationTarget target) => _specs[target];

    public static IReadOnlyList<GenerationTarget> AllTargets => Enum.GetValues<GenerationTarget>();

    public static IReadOnlyList<string> AllFieldNames =>
        [.. AllTargets.SelectMany(Fields).Select(spec => spec.Field).Distinct()];

    public static GenerationFieldSpec? FindField(GenerationTarget target, string field) =>
        Fields(target).FirstOrDefault(spec => string.Equals(spec.Field, field, StringComparison.OrdinalIgnoreCase));

    public static bool HasField(GenerationTarget target, string field) => FindField(target, field) is not null;

    public static string Label(GenerationTarget target) => target switch
    {
        GenerationTarget.World => "World",
        GenerationTarget.ProjectName => "Book name",
        GenerationTarget.Frame => "Frame",
        GenerationTarget.Premise => "Premise",
        GenerationTarget.Character => "Character",
        GenerationTarget.Knowledge => "Knowledge entry",
        GenerationTarget.WorldState => "World state",
        _ => target.ToString(),
    };

    public static string Instruction(GenerationTarget target) => target switch
    {
        GenerationTarget.World => "Invent the story's world: a name and a vivid description of its setting.",
        GenerationTarget.ProjectName => "Suggest a short, evocative title for the book.",
        GenerationTarget.Frame => "Define the story's frame: genre, tone, narrative style, point of view, tense, rating, premise and overall direction. Keep them consistent with each other.",
        GenerationTarget.Premise => "Write a compelling story premise.",
        GenerationTarget.Character => "Create a story character: name, role, age, description, personality, background, goals and traits. Any field may be left empty if it does not apply.",
        GenerationTarget.Knowledge => "Create a story-wiki knowledge entry: choose a kind (Note, Place, Item, Event, Faction, Rule or Background), a short title, a few short tags and a self-contained content body.",
        GenerationTarget.WorldState => "Describe the situation right before chapter 1: when and where the story opens, and what is happening.",
        _ => "Describe the requested field.",
    };

    public static string SchemaName(GenerationTarget target) => $"{target}Options";

    public static JsonObject BuildSchema(GenerationTarget target, int variants)
    {
        var properties = new JsonObject();
        var required = new JsonArray();
        foreach (var spec in Fields(target))
        {
            properties[spec.JsonName] = spec.IsList
                ? new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = new JsonObject { ["type"] = "string" },
                    ["description"] = spec.Description,
                }
                : new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = spec.Description,
                };
            required.Add(spec.JsonName);
        }

        var item = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required,
            ["additionalProperties"] = false,
        };

        return new JsonObject
        {
            ["type"] = "array",
            ["minItems"] = variants,
            ["maxItems"] = variants,
            ["items"] = item,
        };
    }
}
