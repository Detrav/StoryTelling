using System.Text.Json.Nodes;

namespace StoryTelling.Application.Chapters;

public static class EditorChecklistSchema
{
    public static JsonObject Build() => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["checks"] = new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["id"] = new JsonObject { ["type"] = "string" },
                        ["ok"] = new JsonObject { ["type"] = "boolean" },
                        ["reason"] = new JsonObject { ["type"] = "string" },
                    },
                    ["required"] = new JsonArray { "id", "ok", "reason" },
                    ["additionalProperties"] = false,
                },
            },
        },
        ["required"] = new JsonArray { "checks" },
        ["additionalProperties"] = false,
    };
}
