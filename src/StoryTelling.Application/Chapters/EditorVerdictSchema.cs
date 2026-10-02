using System.Text.Json.Nodes;
using StoryTelling.Application.Review;

namespace StoryTelling.Application.Chapters;

public static class EditorVerdictSchema
{
    public static JsonObject Build() => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["issues"] = new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject
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
                },
            },
        },
        ["required"] = new JsonArray { "issues" },
        ["additionalProperties"] = false,
    };
}
