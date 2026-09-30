namespace StoryTelling.Domain;

public sealed class AssistantMessage
{
    public AssistantRole Role { get; set; }

    public string Content { get; set; } = string.Empty;

    public DateTimeOffset CreatedUtc { get; set; }
}
