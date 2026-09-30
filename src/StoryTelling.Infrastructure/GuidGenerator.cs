using StoryTelling.Application.Abstractions;

namespace StoryTelling.Infrastructure;

public sealed class GuidGenerator : IGuidGenerator
{
    public Guid NewGuid() => Guid.NewGuid();
}
