using System.Text.Json.Serialization;

namespace StoryTelling.Tests;

internal sealed record ProbeModel(string Answer);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ProbeModel))]
internal partial class TestJsonContext : JsonSerializerContext
{
}
