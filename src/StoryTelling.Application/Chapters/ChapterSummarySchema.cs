using System.Text.Json.Nodes;
using StoryTelling.Application.Review;
using StoryTelling.Domain;

namespace StoryTelling.Application.Chapters;

public static class ChapterSummarySchema
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
                ["entryId"] = new JsonObject { ["type"] = "string" },
                ["status"] = new JsonObject
                {
                    ["type"] = "string",
                    ["enum"] = new JsonArray(["None", .. Enum.GetNames<KnowledgeStatus>().Select(name => (JsonNode)name)]),
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
            ["required"] = new JsonArray { "operation", "entryId", "status", "title", "kind", "tags", "content", "reason" },
            ["additionalProperties"] = false,
        };

        var continuity = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["severity"] = new JsonObject
                {
                    ["type"] = "string",
                    ["enum"] = new JsonArray([.. Enum.GetNames<ReviewSeverity>().Select(name => (JsonNode)name)]),
                },
                ["detail"] = new JsonObject { ["type"] = "string" },
                ["reference"] = new JsonObject { ["type"] = "string" },
            },
            ["required"] = new JsonArray { "severity", "detail", "reference" },
            ["additionalProperties"] = false,
        };

        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["logline"] = new JsonObject { ["type"] = "string" },
                ["timeAndPlace"] = new JsonObject { ["type"] = "string" },
                ["situation"] = new JsonObject { ["type"] = "string" },
                ["knowledgeChanges"] = new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = change,
                },
                ["continuityNotes"] = new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = continuity,
                },
                ["directionRewrites"] = new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JsonObject
                        {
                            ["chapterNumber"] = new JsonObject { ["type"] = "integer" },
                            ["direction"] = new JsonObject { ["type"] = "string" },
                        },
                        ["required"] = new JsonArray { "chapterNumber", "direction" },
                        ["additionalProperties"] = false,
                    },
                },
            },
            ["required"] = new JsonArray { "logline", "timeAndPlace", "situation", "knowledgeChanges", "continuityNotes", "directionRewrites" },
            ["additionalProperties"] = false,
        };
    }
}
