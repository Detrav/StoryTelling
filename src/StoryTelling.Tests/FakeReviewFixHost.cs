using StoryTelling.Application.Generation;
using StoryTelling.Application.Review;
using StoryTelling.ViewModels;

namespace StoryTelling.Tests;

internal sealed class FakeReviewFixHost : IReviewFixHost
{
    public IReadOnlyList<ReviewChange> Changes { get; set; } = [];

    public ReviewFix? LastApplied { get; private set; }

    public string? LastLabel { get; private set; }

    public GenerationTarget? LastTarget { get; private set; }

    public IReadOnlyList<ReviewChange> PreviewFix(ReviewFix fix) => Changes;

    public string? SingleReference(GenerationTarget target) => SingleReferences.TryGetValue(target, out var reference) ? reference : null;

    public Dictionary<GenerationTarget, string> SingleReferences { get; } = [];

    public List<ReviewFixTarget> Targets { get; } = [new(GenerationTarget.Frame, string.Empty, "Frame")];

    public IReadOnlyList<ReviewFixTarget> FixTargets() => Targets;

    public void ApplyFix(ReviewFix fix, string label)
    {
        LastApplied = fix;
        LastLabel = label;
    }

    public Task<IReadOnlyList<GenerationOption>> GenerateAsync(
        GenerationTarget target,
        string brief,
        int options,
        GenerationSession session,
        IProgress<GenerationProgress>? progress,
        CancellationToken cancellationToken)
    {
        LastTarget = target;
        return Task.FromResult<IReadOnlyList<GenerationOption>>([]);
    }
}
