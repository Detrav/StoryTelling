using StoryTelling.Domain;

namespace StoryTelling.Application.Story;

public sealed record KnowledgeSummary(Guid Id, KnowledgeKind Kind, string Title, IReadOnlyList<string> Tags);
