using System.Text.Json.Nodes;
using StoryTelling.Domain;

namespace StoryTelling.Application.Knowledge;

public static class KnowledgeImportSchema
{
    public static JsonObject Build()
    {
        var entry = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["kind"] = new JsonObject
                {
                    ["type"] = "string",
                    ["enum"] = new JsonArray([.. Enum.GetNames<KnowledgeKind>().Select(name => (JsonNode)name)]),
                },
                ["title"] = new JsonObject { ["type"] = "string" },
                ["tags"] = new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = new JsonObject { ["type"] = "string" },
                },
                ["content"] = new JsonObject { ["type"] = "string" },
            },
            ["required"] = new JsonArray { "kind", "title", "tags", "content" },
            ["additionalProperties"] = false,
        };

        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["entries"] = new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = entry,
                },
            },
            ["required"] = new JsonArray { "entries" },
            ["additionalProperties"] = false,
        };
    }
}
