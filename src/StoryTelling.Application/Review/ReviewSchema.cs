using System.Text.Json.Nodes;
using StoryTelling.Application.Generation;
using StoryTelling.Domain;

namespace StoryTelling.Application.Review;

public static class ReviewSchema
{
    public static JsonObject Build(bool includeReconciliation = true)
    {
        var knowledgeFields = GenerationTargets.Fields(GenerationTarget.Knowledge).Select(spec => spec.Field).ToList();

        var edit = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["op"] = new JsonObject
                {
                    ["type"] = "string",
                    ["enum"] = new JsonArray([.. Enum.GetNames<ReviewEditOperation>().Select(name => (JsonNode)name)]),
                },
                ["target"] = new JsonObject
                {
                    ["type"] = "string",
                    ["enum"] = new JsonArray { GenerationTarget.Knowledge.ToString() },
                },
                ["reference"] = new JsonObject { ["type"] = "string" },
                ["field"] = new JsonObject
                {
                    ["type"] = "string",
                    ["enum"] = new JsonArray([.. knowledgeFields.Select(field => (JsonNode)field)]),
                },
                ["value"] = new JsonObject { ["type"] = "string" },
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
            },
            ["required"] = new JsonArray { "op", "reference" },
            ["additionalProperties"] = false,
        };

        var finding = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["severity"] = new JsonObject
                {
                    ["type"] = "string",
                    ["enum"] = new JsonArray([.. Enum.GetNames<ReviewSeverity>().Select(name => (JsonNode)name)]),
                },
                ["area"] = new JsonObject
                {
                    ["type"] = "string",
                    ["enum"] = new JsonArray([.. Enum.GetNames<ReviewArea>().Select(name => (JsonNode)name)]),
                },
                ["title"] = new JsonObject { ["type"] = "string" },
                ["detail"] = new JsonObject { ["type"] = "string" },
                ["suggestion"] = new JsonObject { ["type"] = "string" },
                ["reference"] = new JsonObject { ["type"] = "string" },
                ["fix"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["edits"] = new JsonObject
                        {
                            ["type"] = "array",
                            ["items"] = edit,
                        },
                    },
                    ["required"] = new JsonArray { "edits" },
                    ["additionalProperties"] = false,
                },
            },
            ["required"] = new JsonArray { "severity", "area", "title", "detail", "suggestion", "reference" },
            ["additionalProperties"] = false,
        };

        var properties = new JsonObject();
        if (includeReconciliation)
        {
            properties["reconciliation"] = new JsonObject
            {
                ["type"] = "array",
                ["description"] = "For each person, every age/date statement with its source and the implied birth year, used to check consistency.",
                ["items"] = new JsonObject { ["type"] = "string" },
            };
        }

        properties["findings"] = new JsonObject
        {
            ["type"] = "array",
            ["items"] = finding,
        };

        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = new JsonArray { "findings" },
            ["additionalProperties"] = false,
        };
    }
}
