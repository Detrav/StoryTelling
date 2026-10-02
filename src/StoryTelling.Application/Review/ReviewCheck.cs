using StoryTelling.Domain;

namespace StoryTelling.Application.Review;

public sealed record ReviewCheck(
    string Id,
    string Label,
    ReviewScope Scope,
    string Intro,
    IReadOnlyList<string> Checklist,
    bool Reconcile = false,
    IReadOnlyList<KnowledgeKind>? Kinds = null,
    bool IncludeWorld = false,
    bool IncludeInitialState = false,
    bool IncludeFullText = true,
    int MaxChars = 8000);
