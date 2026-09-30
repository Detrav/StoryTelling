namespace StoryTelling.Application.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
