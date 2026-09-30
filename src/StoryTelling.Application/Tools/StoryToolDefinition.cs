using System.Text.Json.Nodes;

namespace StoryTelling.Application.Tools;

public sealed record StoryToolDefinition(string Name, string Description, JsonObject Parameters);
