using StoryTelling.Domain;

namespace StoryTelling.Application.Retrieval;

public sealed record KnowledgeFragment(Guid EntryId, string Title, KnowledgeKind Kind, int Index, string Text);
