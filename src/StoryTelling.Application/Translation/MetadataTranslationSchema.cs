using System.Text.Json.Nodes;

namespace StoryTelling.Application.Translation;

public static class MetadataTranslationSchema
{
    public static JsonObject Build()
    {
        var chapterTitle = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["number"] = new JsonObject { ["type"] = "integer" },
                ["title"] = new JsonObject { ["type"] = "string" },
            },
            ["required"] = new JsonArray { "number", "title" },
            ["additionalProperties"] = false,
        };

        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["name"] = new JsonObject { ["type"] = "string" },
                ["annotation"] = new JsonObject { ["type"] = "string" },
                ["chapterTitles"] = new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = chapterTitle,
                },
            },
            ["required"] = new JsonArray { "name", "annotation", "chapterTitles" },
            ["additionalProperties"] = false,
        };
    }
}