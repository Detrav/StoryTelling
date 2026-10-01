namespace StoryTelling.Application.Generation;

public sealed record GenerationFieldSpec(string Field, string JsonName, string Label, string Description, bool IsList = false);
