using StoryTelling.Application.Abstractions;

namespace StoryTelling.Infrastructure;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
