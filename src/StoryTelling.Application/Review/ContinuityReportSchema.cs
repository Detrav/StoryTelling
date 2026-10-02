using System.Text.Json.Nodes;

namespace StoryTelling.Application.Review;

public static class ContinuityReportSchema
{
    public static JsonObject Build() => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["findings"] = new JsonObject
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
                        ["title"] = new JsonObject { ["type"] = "string" },
                        ["detail"] = new JsonObject { ["type"] = "string" },
                        ["reference"] = new JsonObject { ["type"] = "string" },
                    },
                    ["required"] = new JsonArray { "severity", "title", "detail", "reference" },
                    ["additionalProperties"] = false,
                },
            },
        },
        ["required"] = new JsonArray { "findings" },
        ["additionalProperties"] = false,
    };
}
