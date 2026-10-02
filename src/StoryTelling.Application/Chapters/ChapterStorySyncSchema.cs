using System.Text.Json.Nodes;

namespace StoryTelling.Application.Chapters;

public static class ChapterStorySyncSchema
{
    public static JsonObject Build() => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["storySoFar"] = new JsonObject { ["type"] = "string" },
        },
        ["required"] = new JsonArray { "storySoFar" },
        ["additionalProperties"] = false,
    };
}
