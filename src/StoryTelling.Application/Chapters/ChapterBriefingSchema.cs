using System.Text.Json.Nodes;
using StoryTelling.Domain;

namespace StoryTelling.Application.Chapters;

public static class ChapterBriefingSchema
{
    public static JsonObject Build()
    {
        var change = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["operation"] = new JsonObject
                {
                    ["type"] = "string",
                    ["enum"] = new JsonArray([.. Enum.GetNames<KnowledgeChangeOperation>().Select(name => (JsonNode)name)]),
                },
                ["title"] = new JsonObject { ["type"] = "string" },
                ["kind"] = new JsonObject
                {
                    ["type"] = "string",
                    ["enum"] = new JsonArray([.. Enum.GetNames<KnowledgeKind>().Select(name => (JsonNode)name)]),
                },
                ["tags"] = new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = new JsonObject { ["type"] = "string" },
                },
                ["content"] = new JsonObject { ["type"] = "string" },
                ["reason"] = new JsonObject { ["type"] = "string" },
            },
            ["required"] = new JsonArray { "operation", "title", "kind", "tags", "content", "reason" },
            ["additionalProperties"] = false,
        };

        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["logline"] = new JsonObject { ["type"] = "string" },
                ["timeAndPlace"] = new JsonObject { ["type"] = "string" },
                ["description"] = new JsonObject { ["type"] = "string" },
                ["knowledgeChanges"] = new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = change,
                },
            },
            ["required"] = new JsonArray { "logline", "timeAndPlace", "description", "knowledgeChanges" },
            ["additionalProperties"] = false,
        };
    }
}
