using System.Text.Json.Nodes;

namespace StoryTelling.Application.Llm;

public sealed record LlmTool(string Name, string Description, JsonNode Parameters);
