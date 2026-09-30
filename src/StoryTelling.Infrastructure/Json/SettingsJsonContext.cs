using System.Text.Json.Serialization;
using StoryTelling.Application.Settings;

namespace StoryTelling.Infrastructure.Json;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(AppSettings))]
public partial class SettingsJsonContext : JsonSerializerContext
{
}
