using StoryTelling.Application.Generation;
using StoryTelling.Application.Review;
using StoryTelling.ViewModels;

namespace StoryTelling.Tests;

internal sealed class FakeReviewFixHost : IReviewFixHost
{
    public IReadOnlyList<ReviewChange> Changes { get; set; } = [];

    public ReviewFix? LastApplied { get; private set; }

    public string? LastLabel { get; private set; }

    public string? LastProposedReference { get; private set; }

    public IReadOnlyList<ReviewChange> PreviewFix(ReviewFix fix) => Changes.Count > 0
        ? Changes
        : [.. fix.Edits.Select(edit => new ReviewChange(edit, $"{edit.Reference} · {edit.Field}", "old", edit.Value))];

    public string? SingleReference(GenerationTarget target) => SingleReferences.TryGetValue(target, out var reference) ? reference : null;

    public Dictionary<GenerationTarget, string> SingleReferences { get; } = [];

    public List<ReviewFixTarget> Targets { get; } = [new(GenerationTarget.Knowledge, string.Empty, "Knowledge")];

    public IReadOnlyList<ReviewFixTarget> FixTargets() => Targets;

    public IReadOnlyList<GenerationOption> GenerationOptions { get; set; } = [];

    public string Signature { get; set; } = string.Empty;

    public string ReviewSignature() => Signature;

    public int ApplyCount { get; private set; }

    public void ApplyFix(ReviewFix fix, string label)
    {
        LastApplied = fix;
        LastLabel = label;
        ApplyCount++;
    }

    public Task<IReadOnlyList<GenerationOption>> ProposeEntryAsync(string reference, string brief, CancellationToken cancellationToken)
    {
        LastProposedReference = reference;
        return Task.FromResult(GenerationOptions);
    }
}
