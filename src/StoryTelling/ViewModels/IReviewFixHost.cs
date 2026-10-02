using StoryTelling.Application.Generation;
using StoryTelling.Application.Review;
using StoryTelling.Domain;

namespace StoryTelling.ViewModels;

public interface IReviewFixHost
{
    IReadOnlyList<ReviewChange> PreviewFix(ReviewFix fix);

    void ApplyFix(ReviewFix fix, string label);

    void AddEntry(KnowledgeEntry entry, string label);

    string? SingleReference(GenerationTarget target);

    IReadOnlyList<ReviewFixTarget> FixTargets();

    string ReviewSignature();

    Task<IReadOnlyList<GenerationOption>> GenerateAsync(
        GenerationTarget target,
        string brief,
        int options,
        GenerationSession session,
        IProgress<GenerationProgress>? progress,
        CancellationToken cancellationToken);
}
