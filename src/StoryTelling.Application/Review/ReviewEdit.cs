using StoryTelling.Application.Generation;
using StoryTelling.Domain;

namespace StoryTelling.Application.Review;

public sealed record ReviewEdit(
    GenerationTarget Target,
    string Reference,
    string Field,
    string Value,
    ReviewEditOperation Operation = ReviewEditOperation.Set,
    KnowledgeKind? Kind = null,
    IReadOnlyList<string>? Tags = null);
