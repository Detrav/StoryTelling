using StoryTelling.Application.Generation;

namespace StoryTelling.Application.Review;

public sealed record ReviewEdit(GenerationTarget Target, string Reference, string Field, string Value);
