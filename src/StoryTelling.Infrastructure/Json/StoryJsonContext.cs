using System.Text.Json.Serialization;
using StoryTelling.Domain;

namespace StoryTelling.Infrastructure.Json;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(Project))]
public partial class StoryJsonContext : JsonSerializerContext
{
}
