using StoryTelling.Domain;

namespace StoryTelling.Application.Chapters;

public sealed record ChapterSummary(string Logline, WorldState WorldState, IReadOnlyList<KnowledgeChange> KnowledgeChanges);
