using StoryTelling.Domain;

namespace StoryTelling.Application.Review;

public interface IContinuityReviewer
{
    Task<IReadOnlyList<ContinuityFinding>> ReviewAsync(
        Project project,
        Chapter chapter,
        CancellationToken cancellationToken = default);
}
