using System.Text.Json.Nodes;

namespace StoryTelling.Application.Chapters;

public static class ChapterContinuitySchema
{
    public static JsonObject Build() => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["continues"] = new JsonObject { ["type"] = "boolean" },
            ["score"] = new JsonObject { ["type"] = "integer" },
            ["restartSignals"] = new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject { ["type"] = "string" },
            },
            ["contradictions"] = new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject { ["type"] = "string" },
            },
            ["notes"] = new JsonObject { ["type"] = "string" },
        },
        ["required"] = new JsonArray { "continues", "score", "restartSignals", "contradictions", "notes" },
        ["additionalProperties"] = false,
    };
}
