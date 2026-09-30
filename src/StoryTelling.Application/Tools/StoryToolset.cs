using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StoryTelling.Application.Retrieval;
using StoryTelling.Application.Story;
using StoryTelling.Domain;

namespace StoryTelling.Application.Tools;

public sealed class StoryToolset
{
    private const int _defaultTopK = 5;

    private readonly StoryQuery _query;

    public StoryToolset(StoryQuery query)
    {
        _query = query;
        Definitions = BuildDefinitions();
    }

    public IReadOnlyList<StoryToolDefinition> Definitions { get; }

    public string Invoke(string name, string argumentsJson)
    {
        var arguments = ParseArguments(argumentsJson);
        return name switch
        {
            "story" => DescribeStory(_query.Story()),
            "characters" => DescribeCharacters(_query.Characters()),
            "character" => DescribeCharacter(_query.Character(Required(arguments, "name"))),
            "world_state" => DescribeWorldState(_query.WorldState()),
            "recent_loglines" => DescribeLoglines(_query.RecentLoglines(OptionalInt(arguments, "count", 3))),
            "list_entries" => DescribeEntries(_query.ListEntries(OptionalKind(arguments, "kind"))),
            "get_entry" => DescribeEntry(_query.GetEntry(Required(arguments, "key"))),
            "search_knowledge" => DescribeFragments(_query.SearchKnowledge(
                Required(arguments, "query"),
                OptionalKind(arguments, "kind"),
                OptionalInt(arguments, "topK", _defaultTopK))),
            _ => throw new InvalidOperationException($"Unknown tool '{name}'."),
        };
    }

    private static IReadOnlyList<StoryToolDefinition> BuildDefinitions() =>
    [
        new("story", "The story frame (name, genre, tone, style, point of view, tense, rating, premise, direction) and the world lore.", EmptySchema()),
        new("characters", "The cast: every character's name and role.", EmptySchema()),
        new("character", "A single character's full profile by name.", ObjectSchema(
            [("name", StringProperty("The character's name."))],
            ["name"])),
        new("world_state", "The current situation: time and place plus a free-form description.", EmptySchema()),
        new("recent_loglines", "The loglines of the most recent chapters, oldest first.", ObjectSchema(
            [("count", IntegerProperty("How many recent loglines to return (default 3)."))],
            [])),
        new("list_entries", "List knowledge entries (title, kind, tags). Optionally filter by kind.", ObjectSchema(
            [("kind", EnumProperty("Optional knowledge kind filter."))],
            [])),
        new("get_entry", "The full content of one knowledge entry by id or title.", ObjectSchema(
            [("key", StringProperty("The entry's id or title."))],
            ["key"])),
        new("search_knowledge", "Search the knowledge base and return the most relevant fragments.", ObjectSchema(
            [
                ("query", StringProperty("What to search for.")),
                ("kind", EnumProperty("Optional knowledge kind filter.")),
                ("topK", IntegerProperty("How many fragments to return (default 5).")),
            ],
            ["query"])),
    ];

    private static JsonObject EmptySchema() => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject(),
        ["additionalProperties"] = false,
    };

    private static JsonObject ObjectSchema(IReadOnlyList<(string Name, JsonObject Property)> properties, IReadOnlyList<string> required)
    {
        var propertyNode = new JsonObject();
        foreach (var (name, property) in properties)
        {
            propertyNode[name] = property;
        }

        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = propertyNode,
            ["required"] = new JsonArray([.. required.Select(name => (JsonNode)name)]),
            ["additionalProperties"] = false,
        };
    }

    private static JsonObject StringProperty(string description) => new() { ["type"] = "string", ["description"] = description };

    private static JsonObject IntegerProperty(string description) => new() { ["type"] = "integer", ["description"] = description };

    private static JsonObject EnumProperty(string description) => new()
    {
        ["type"] = "string",
        ["description"] = description,
        ["enum"] = new JsonArray([.. Enum.GetNames<KnowledgeKind>().Select(name => (JsonNode)name)]),
    };

    private static JsonObject ParseArguments(string argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson))
        {
            return new JsonObject();
        }

        try
        {
            return JsonNode.Parse(argumentsJson) as JsonObject ?? new JsonObject();
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("Tool arguments are not valid JSON.", exception);
        }
    }

    private static string Required(JsonObject arguments, string name)
    {
        var value = StringOrNull(arguments, name);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Missing required argument '{name}'.");
        }

        return value;
    }

    private static string? StringOrNull(JsonObject arguments, string name) =>
        arguments.TryGetPropertyValue(name, out var node) && node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static int OptionalInt(JsonObject arguments, string name, int fallback) =>
        arguments.TryGetPropertyValue(name, out var node) && node is JsonValue value && value.TryGetValue<int>(out var number) ? number : fallback;

    private static KnowledgeKind? OptionalKind(JsonObject arguments, string name) =>
        StringOrNull(arguments, name) is { Length: > 0 } text && Enum.TryParse<KnowledgeKind>(text, ignoreCase: true, out var kind) ? kind : null;

    private static string DescribeStory(StoryOverview story)
    {
        var builder = new StringBuilder();
        Append(builder, "Book", story.Name);
        Append(builder, "Genre", story.Genre);
        Append(builder, "Tone", story.Tone);
        Append(builder, "Style", story.Style);
        Append(builder, "Point of view", story.PointOfView);
        Append(builder, "Tense", story.Tense);
        Append(builder, "Rating", story.Rating);
        Append(builder, "Premise", story.Premise);
        Append(builder, "Direction", story.Direction);
        Append(builder, "World", story.LoreTitle);
        Append(builder, "World details", story.LoreBody);
        return builder.ToString().TrimEnd();
    }

    private static string DescribeCharacters(IReadOnlyList<CharacterSummary> characters)
    {
        if (characters.Count == 0)
        {
            return "No characters yet.";
        }

        return string.Join('\n', characters.Select(character =>
            string.IsNullOrWhiteSpace(character.Role) ? $"- {character.Name}" : $"- {character.Name} ({character.Role})"));
    }

    private static string DescribeCharacter(Character? character)
    {
        if (character is null)
        {
            return "Character not found.";
        }

        var builder = new StringBuilder();
        Append(builder, "Name", character.Name);
        Append(builder, "Role", character.Role);
        Append(builder, "Age", character.Age);
        Append(builder, "Description", character.Description);
        Append(builder, "Personality", character.Personality);
        Append(builder, "Background", character.Background);
        Append(builder, "Goals", character.Goals);
        if (character.Traits.Count > 0)
        {
            Append(builder, "Traits", string.Join(", ", character.Traits));
        }

        return builder.ToString().TrimEnd();
    }

    private static string DescribeWorldState(WorldState state)
    {
        var builder = new StringBuilder();
        Append(builder, "Time and place", state.TimeAndPlace);
        Append(builder, "Description", state.Description);
        return builder.Length == 0 ? "No world state." : builder.ToString().TrimEnd();
    }

    private static string DescribeLoglines(IReadOnlyList<ChapterLogline> loglines)
    {
        if (loglines.Count == 0)
        {
            return "No chapter loglines yet.";
        }

        return string.Join('\n', loglines.Select(logline => $"Ch {logline.Number} ({logline.Title}): {logline.Logline}"));
    }

    private static string DescribeEntries(IReadOnlyList<KnowledgeSummary> entries)
    {
        if (entries.Count == 0)
        {
            return "No knowledge entries.";
        }

        return string.Join('\n', entries.Select(entry =>
        {
            var tags = entry.Tags.Count > 0 ? $" [{string.Join(", ", entry.Tags)}]" : string.Empty;
            return $"- [{entry.Kind}] {entry.Title}{tags}";
        }));
    }

    private static string DescribeEntry(KnowledgeEntry? entry)
    {
        if (entry is null)
        {
            return "Entry not found.";
        }

        var builder = new StringBuilder();
        Append(builder, "Title", entry.Title);
        Append(builder, "Kind", entry.Kind.ToString());
        if (entry.Tags.Count > 0)
        {
            Append(builder, "Tags", string.Join(", ", entry.Tags));
        }

        if (!string.IsNullOrWhiteSpace(entry.Content))
        {
            builder.AppendLine();
            builder.AppendLine(entry.Content.Trim());
        }

        return builder.ToString().TrimEnd();
    }

    private static string DescribeFragments(IReadOnlyList<KnowledgeFragment> fragments)
    {
        if (fragments.Count == 0)
        {
            return "No matching fragments.";
        }

        return string.Join("\n\n", fragments.Select(fragment => $"[{fragment.Title}]\n{fragment.Text}"));
    }

    private static void Append(StringBuilder builder, string label, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            builder.AppendLine($"{label}: {value.Trim()}");
        }
    }
}
