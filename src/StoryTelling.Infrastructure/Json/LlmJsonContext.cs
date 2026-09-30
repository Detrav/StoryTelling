using System.Text.Json.Serialization;
using StoryTelling.Infrastructure.Llm;

namespace StoryTelling.Infrastructure.Json;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ChatCompletionRequestPayload))]
[JsonSerializable(typeof(ChatCompletionResponsePayload))]
internal partial class LlmJsonContext : JsonSerializerContext
{
}
