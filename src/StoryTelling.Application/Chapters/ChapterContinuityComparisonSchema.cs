using System.Text.Json.Nodes;

namespace StoryTelling.Application.Chapters;

public static class ChapterContinuityComparisonSchema
{
    public static JsonObject Build() => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["winner"] = new JsonObject
            {
                ["type"] = "string",
                ["enum"] = new JsonArray(["A", "B", "tie"]),
            },
            ["reasons"] = new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject { ["type"] = "string" },
            },
        },
        ["required"] = new JsonArray { "winner", "reasons" },
        ["additionalProperties"] = false,
    };
}
