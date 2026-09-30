using StoryTelling.Application.Abstractions;

namespace StoryTelling.Tests;

internal sealed class FakeClock : IClock
{
    public FakeClock(DateTimeOffset now) => UtcNow = now;

    public DateTimeOffset UtcNow { get; set; }
}
