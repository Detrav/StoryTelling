using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace StoryTelling.Infrastructure.Llm;

internal sealed class ChatCompletionRequestPayload
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<ChatMessagePayload> Messages { get; set; } = [];

    [JsonPropertyName("temperature")]
    public double Temperature { get; set; }

    [JsonPropertyName("max_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxTokens { get; set; }

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }

    [JsonPropertyName("response_format")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ResponseFormatPayload? ResponseFormat { get; set; }
}

internal sealed class ChatMessagePayload
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

internal sealed class ResponseFormatPayload
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "json_object";

    [JsonPropertyName("json_schema")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonSchemaPayload? JsonSchema { get; set; }
}

internal sealed class JsonSchemaPayload
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("strict")]
    public bool Strict { get; set; } = true;

    [JsonPropertyName("schema")]
    public JsonNode? Schema { get; set; }
}

internal sealed class ChatCompletionResponsePayload
{
    [JsonPropertyName("choices")]
    public List<ChatChoicePayload>? Choices { get; set; }

    [JsonPropertyName("usage")]
    public ChatUsagePayload? Usage { get; set; }

    [JsonPropertyName("error")]
    public ChatErrorPayload? Error { get; set; }
}

internal sealed class ChatChoicePayload
{
    [JsonPropertyName("message")]
    public ChatMessagePayload? Message { get; set; }

    [JsonPropertyName("delta")]
    public ChatMessagePayload? Delta { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

internal sealed class ChatUsagePayload
{
    [JsonPropertyName("prompt_tokens")]
    public int? PromptTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int? CompletionTokens { get; set; }
}

internal sealed class ChatErrorPayload
{
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }
}
