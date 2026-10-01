using StoryTelling.Application.Generation;
using StoryTelling.Application.Review;

namespace StoryTelling.ViewModels;

public interface IReviewFixHost
{
    IReadOnlyList<ReviewChange> PreviewFix(ReviewFix fix);

    void ApplyFix(ReviewFix fix, string label);

    string? SingleReference(GenerationTarget target);

    IReadOnlyList<ReviewFixTarget> FixTargets();

    Task<IReadOnlyList<GenerationOption>> GenerateAsync(
        GenerationTarget target,
        string brief,
        int options,
        GenerationSession session,
        IProgress<GenerationProgress>? progress,
        CancellationToken cancellationToken);
}
