using System.Text.Json.Nodes;
using StoryTelling.Domain;

namespace StoryTelling.Application.Chapters;

public static class EditorNotesSchema
{
    public static JsonObject Build() => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["changes"] = new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["kind"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["enum"] = new JsonArray([.. Enum.GetNames<EditorNoteKind>().Select(name => (JsonNode)name)]),
                        },
                        ["note"] = new JsonObject { ["type"] = "string" },
                    },
                    ["required"] = new JsonArray { "kind", "note" },
                    ["additionalProperties"] = false,
                },
            },
        },
        ["required"] = new JsonArray { "changes" },
        ["additionalProperties"] = false,
    };
}
