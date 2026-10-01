using StoryTelling.Application.Generation;

namespace StoryTelling.ViewModels;

public sealed record ReviewFixTarget(GenerationTarget Target, string Reference, string Label);
