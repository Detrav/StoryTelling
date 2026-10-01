using StoryTelling.Application.Generation;
using StoryTelling.Application.Review;
using StoryTelling.Domain;

namespace StoryTelling.Tests;

internal sealed class FakeProjectReviewAssistant : IProjectReviewAssistant
{
    public IReadOnlyList<ReviewFinding> Findings { get; set; } = [];

    public Project? LastSnapshot { get; private set; }

    public string? LastBrief { get; private set; }

    public Task<IReadOnlyList<ReviewFinding>> ReviewAsync(
        Project snapshot,
        string brief,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        LastSnapshot = snapshot;
        LastBrief = brief;
        return Task.FromResult(Findings);
    }
}
