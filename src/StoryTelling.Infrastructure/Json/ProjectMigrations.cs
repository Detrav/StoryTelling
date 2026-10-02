using System.Text.Json;
using System.Text.Json.Nodes;
using StoryTelling.Domain;

namespace StoryTelling.Infrastructure.Json;

public static class ProjectMigrations
{
    public static string Migrate(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("schemaVersion", out var versionElement)
            || !versionElement.TryGetInt32(out var version)
            || version >= ProjectSchema.Version)
        {
            return json;
        }

        var root = JsonNode.Parse(json) as JsonObject
            ?? throw new InvalidDataException("The project file is not a JSON object.");

        if (version < 2)
        {
            MigrateV1ToV2(root);
        }

        if (version < 6)
        {
            MigrateV5ToV6(root);
        }

        root["schemaVersion"] = ProjectSchema.Version;
        return root.ToJsonString();
    }

    private static void MigrateV5ToV6(JsonObject root)
    {
        RenameSituation(root["initialWorldState"] as JsonObject);

        if (root["chapters"] is JsonArray chapters)
        {
            foreach (var chapter in chapters.OfType<JsonObject>())
            {
                RenameSituation(chapter["worldState"] as JsonObject);
                chapter.Remove("storySoFar");
            }
        }
    }

    private static void RenameSituation(JsonObject? state)
    {
        if (state is null || !state.TryGetPropertyValue("description", out var description))
        {
            return;
        }

        state["situation"] = description?.DeepClone();
        state.Remove("description");
    }

    private static void MigrateV1ToV2(JsonObject root)
    {
        var frame = AsObject(root["frame"]);
        var lore = AsObject(root["lore"]);

        var world = new JsonObject
        {
            ["title"] = Text(lore, "title"),
            ["body"] = Text(lore, "body"),
            ["tags"] = CopyArray(lore, "tags"),
            ["genre"] = Text(frame, "genre"),
            ["tone"] = Text(frame, "tone"),
            ["style"] = Text(frame, "style"),
            ["pointOfView"] = Text(frame, "pointOfView"),
            ["tense"] = Text(frame, "tense"),
            ["rating"] = Text(frame, "rating"),
        };
        root["world"] = world;

        root["initialWorldState"] = root["worldState"]?.DeepClone() ?? new JsonObject();

        var knowledge = root["knowledge"] as JsonArray ?? [];
        if (root["characters"] is JsonArray characters)
        {
            foreach (var entry in characters.OfType<JsonObject>())
            {
                knowledge.Add(CharacterToEntry(entry));
            }
        }

        root["knowledge"] = knowledge;

        root.Remove("frame");
        root.Remove("lore");
        root.Remove("characters");
        root.Remove("worldState");
    }

    private static JsonObject CharacterToEntry(JsonObject character)
    {
        var tags = new JsonArray();
        if (Text(character, "role") is { Length: > 0 } role)
        {
            tags.Add(role);
        }

        if (character["traits"] is JsonArray traits)
        {
            foreach (var trait in traits)
            {
                if (trait is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text))
                {
                    tags.Add(text.Trim());
                }
            }
        }

        var lines = new List<string>();
        AppendLine(lines, Text(character, "description"));
        AppendLabeled(lines, "Personality", Text(character, "personality"));
        AppendLabeled(lines, "Background", Text(character, "background"));
        AppendLabeled(lines, "Goals", Text(character, "goals"));
        AppendLabeled(lines, "Age", Text(character, "age"));

        return new JsonObject
        {
            ["id"] = character["id"]?.DeepClone() ?? Guid.NewGuid(),
            ["kind"] = nameof(KnowledgeKind.Character),
            ["title"] = Text(character, "name"),
            ["tags"] = tags,
            ["content"] = string.Join("\n", lines),
        };
    }

    private static void AppendLine(List<string> lines, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            lines.Add(value.Trim());
        }
    }

    private static void AppendLabeled(List<string> lines, string label, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            lines.Add($"{label}: {value.Trim()}");
        }
    }

    private static JsonObject AsObject(JsonNode? node) => node as JsonObject ?? new JsonObject();

    private static string Text(JsonObject source, string property) =>
        source[property] is JsonValue value && value.TryGetValue<string>(out var text) ? text : string.Empty;

    private static JsonArray CopyArray(JsonObject source, string property) =>
        source[property] is JsonArray array ? (JsonArray)array.DeepClone() : [];
}
