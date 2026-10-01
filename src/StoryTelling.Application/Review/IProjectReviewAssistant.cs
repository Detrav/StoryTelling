using StoryTelling.Domain;

namespace StoryTelling.Application.Review;

public interface IProjectReviewAssistant
{
    Task<IReadOnlyList<ReviewFinding>> ReviewAsync(
        Project snapshot,
        string brief,
        IProgress<Generation.GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
