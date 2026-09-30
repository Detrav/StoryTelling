using System.Text.Json.Nodes;

namespace StoryTelling.Application.Generation;

public static class GenerationTargets
{
    private static readonly IReadOnlyDictionary<GenerationTarget, IReadOnlyList<GenerationFieldSpec>> _specs =
        new Dictionary<GenerationTarget, IReadOnlyList<GenerationFieldSpec>>
        {
            [GenerationTarget.World] =
            [
                new("WorldTitle", "title", "A short, evocative name for the story's world or setting."),
                new("WorldBody", "body", "A vivid description of the world and setting (2-5 sentences)."),
            ],
            [GenerationTarget.ProjectName] =
            [
                new("ProjectName", "name", "A short, evocative title for the book."),
            ],
            [GenerationTarget.Premise] =
            [
                new("Premise", "premise", "A compelling story premise (2-4 sentences)."),
            ],
            [GenerationTarget.Characters] =
            [
                new("Characters", "characters", "A cast of main characters: each a name plus a one-line description."),
            ],
            [GenerationTarget.ExtraFiles] =
            [
                new("ExtraFiles", "notes", "Concise author's notes about the story's background."),
            ],
            [GenerationTarget.WorldState] =
            [
                new("WorldState", "worldState", "The initial world state: time and place, characters present, active threads, notable items and open questions."),
            ],
        };

    public static IReadOnlyList<GenerationFieldSpec> Fields(GenerationTarget target) => _specs[target];

    public static string Label(GenerationTarget target) => target switch
    {
        GenerationTarget.World => "World",
        GenerationTarget.ProjectName => "Book name",
        GenerationTarget.Premise => "Premise",
        GenerationTarget.Characters => "Characters",
        GenerationTarget.ExtraFiles => "Extra file",
        GenerationTarget.WorldState => "World state",
        _ => target.ToString(),
    };

    public static string Instruction(GenerationTarget target) => target switch
    {
        GenerationTarget.World => "Invent the story's world: a name and a vivid description of its setting.",
        GenerationTarget.ProjectName => "Suggest a short, evocative title for the book.",
        GenerationTarget.Premise => "Write a compelling story premise.",
        GenerationTarget.Characters => "Propose a cast of main characters.",
        GenerationTarget.ExtraFiles => "Draft concise author's notes about the story's background.",
        GenerationTarget.WorldState => "Describe the initial world state of the story.",
        _ => "Describe the requested field.",
    };

    public static string SchemaName(GenerationTarget target) => $"{target}Options";

    public static JsonObject BuildSchema(GenerationTarget target, int variants)
    {
        var properties = new JsonObject();
        var required = new JsonArray();
        foreach (var spec in Fields(target))
        {
            properties[spec.JsonName] = new JsonObject
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
