using StoryTelling.Domain;

namespace StoryTelling.Application.Review;

public interface IProjectReviewAssistant
{
    Task<IReadOnlyList<ReviewFinding>> ReviewAsync(
        Project snapshot,
        string brief,
        ReviewCheck check,
        IProgress<Generation.GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
