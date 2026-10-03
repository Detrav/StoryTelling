using StoryTelling.Application.Generation;
using StoryTelling.Application.Review;

namespace StoryTelling.ViewModels;

public interface IReviewFixHost
{
    IReadOnlyList<ReviewChange> PreviewFix(ReviewFix fix);

    void ApplyFix(ReviewFix fix, string label);

    string? SingleReference(GenerationTarget target);

    IReadOnlyList<ReviewFixTarget> FixTargets();

    string ReviewSignature();

    Task<IReadOnlyList<GenerationOption>> ProposeEntryAsync(
        string reference,
        string brief,
        CancellationToken cancellationToken);
}
