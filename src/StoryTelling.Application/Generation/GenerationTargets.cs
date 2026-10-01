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
                new("Genre", "genre", "Genre", "The story's genre, e.g. 'dark fantasy'."),
                new("Tone", "tone", "Tone", "The overall tone and mood, e.g. 'grim, hopeful, lyrical'."),
                new("Style", "style", "Style", "The narrative voice, e.g. 'terse, atmospheric, sensory-heavy'."),
                new("PointOfView", "pointOfView", "Point of view", "e.g. 'third person limited', 'first person'."),
                new("Tense", "tense", "Tense", "e.g. 'past', 'present'."),
                new("Rating", "rating", "Rating", "Content rating, e.g. 'PG-13', 'R'."),
            ],
            [GenerationTarget.ProjectName] =
            [
                new("ProjectName", "name", "Book name", "A short, evocative title for the book."),
            ],
            [GenerationTarget.Knowledge] =
            [
                new("Kind", "kind", "Kind", "One of: Note, Character, Place, Item, Event, Faction, Rule, Background."),
                new("Title", "title", "Title", "A short, specific name for the entry."),
                new("Tags", "tags", "Tags", "A few short tags, e.g. 'city', 'port', 'trade'. Never write sentences.", IsList: true),
                new("Content", "content", "Content", "A self-contained description of the entry; keep the important details."),
            ],
            [GenerationTarget.InitialWorldState] =
            [
                new("TimeAndPlace", "timeAndPlace", "Time and place", "When and where the story opens (a short phrase)."),
                new("Description", "description", "Description", "The situation before chapter 1 in free form: what is happening, who is involved, notable facts, tensions and open questions."),
            ],
            [GenerationTarget.ChapterSettings] =
            [
                new("Title", "title", "Chapter title", "A short, evocative chapter title."),
                new("Direction", "direction", "Direction", "What should happen in this chapter (2-4 sentences), continuing coherently from the current state and the previous chapters."),
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
        GenerationTarget.Knowledge => "Knowledge entry",
        GenerationTarget.InitialWorldState => "Initial world state",
        GenerationTarget.ChapterSettings => "Chapter",
        _ => target.ToString(),
    };

    public static string Instruction(GenerationTarget target) => target switch
    {
        GenerationTarget.World => "Define the story's world: a title, a vivid description of the setting, and the narrative frame (genre, tone, style, point of view, tense, rating). Keep them consistent with each other.",
        GenerationTarget.ProjectName => "Suggest a short, evocative title for the book.",
        GenerationTarget.Knowledge => "Create a story-wiki knowledge entry: choose a kind (Note, Character, Place, Item, Event, Faction, Rule or Background), a short title, a few short tags and a self-contained content body. For a character, use the kind Character, the name as the title and a free-form description of who they are as the content. Never reuse a name that already exists in the knowledge base.",
        GenerationTarget.InitialWorldState => "Describe the situation right before chapter 1: when and where the story opens, and what is happening.",
        GenerationTarget.ChapterSettings => "Propose a chapter title and a direction for this chapter. Continue coherently from the current state, the previous chapters and the knowledge base; consult the project with the tools first.",
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
