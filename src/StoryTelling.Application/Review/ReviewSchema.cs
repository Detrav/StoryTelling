using System.Text.Json.Nodes;
using StoryTelling.Application.Generation;

namespace StoryTelling.Application.Review;

public static class ReviewSchema
{
    public static JsonObject Build()
    {
        var knowledgeFields = GenerationTargets.Fields(GenerationTarget.Knowledge).Select(spec => spec.Field).ToList();

        var edit = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
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
            },
            ["required"] = new JsonArray { "target", "reference", "field", "value" },
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
            ["required"] = new JsonArray { "severity", "area", "title", "detail", "suggestion", "reference", "fix" },
            ["additionalProperties"] = false,
        };

        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["findings"] = new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = finding,
                },
            },
            ["required"] = new JsonArray { "findings" },
            ["additionalProperties"] = false,
        };
    }
}
