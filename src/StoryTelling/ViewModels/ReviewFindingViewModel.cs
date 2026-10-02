using CommunityToolkit.Mvvm.ComponentModel;
using StoryTelling.Application.Generation;
using StoryTelling.Application.Review;

namespace StoryTelling.ViewModels;

public partial class ReviewFindingViewModel : ObservableObject
{
    private static readonly string[] _creationMarkers =
    [
        "no dedicated", "no entry", "has no entry", "no knowledge entry", "missing entry",
        "dangling reference", "undefined reference", "does not exist", "never defined",
        "no character entry", "no such entry", "is referenced but", "is mentioned but",
    ];

    public ReviewFindingViewModel(ReviewFinding finding, Func<GenerationTarget, string?>? singleReference = null, bool canFixWithAi = true)
    {
        Finding = finding;
        Action = ClassifyAction(finding, canFixWithAi);

        if (ExplicitTarget(finding.Area, finding.Reference) is { } target)
        {
            AiTarget = target;
            AiReference = finding.Reference ?? string.Empty;
        }
        else if (singleReference is not null
            && FallbackTarget(finding.Area) is { } fallback
            && singleReference(fallback) is { Length: > 0 } reference)
        {
            AiTarget = fallback;
            AiReference = reference;
        }
    }

    public ReviewFinding Finding { get; }

    public ReviewFindingAction Action { get; }

    public string Severity => Finding.Severity.ToString();

    public string Area => Finding.Area.ToString();

    public string Title => Finding.Title;

    public string Detail => Finding.Detail;

    public string? Suggestion => Finding.Suggestion;

    public bool HasSuggestion => !string.IsNullOrWhiteSpace(Finding.Suggestion);

    public bool CanApplyFix => Action == ReviewFindingAction.Fix;

    public bool CanFixWithAi => Action == ReviewFindingAction.FixWithAi;

    public bool CanCreateEntry => Action == ReviewFindingAction.CreateEntry;

    public GenerationTarget? AiTarget { get; }

    public string AiReference { get; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FixLabel))]
    private bool _isFixed;

    public string FixLabel => IsFixed ? "Fixed" : "Fix";

    private static ReviewFindingAction ClassifyAction(ReviewFinding finding, bool canFixWithAi)
    {
        if (finding.Fix is { IsEmpty: false })
        {
            return ReviewFindingAction.Fix;
        }

        if (IsCreationIssue(finding))
        {
            return ReviewFindingAction.CreateEntry;
        }

        return canFixWithAi ? ReviewFindingAction.FixWithAi : ReviewFindingAction.None;
    }

    private static bool IsCreationIssue(ReviewFinding finding)
    {
        if (finding.Suggestion?.TrimStart().StartsWith("Create", StringComparison.OrdinalIgnoreCase) == true)
        {
            return true;
        }

        var text = $"{finding.Title} {finding.Detail}".ToLowerInvariant();
        return _creationMarkers.Any(text.Contains);
    }

    private static GenerationTarget? ExplicitTarget(ReviewArea area, string? reference) => area switch
    {
        ReviewArea.Knowledge => string.IsNullOrWhiteSpace(reference) ? null : GenerationTarget.Knowledge,
        _ => null,
    };

    private static GenerationTarget? FallbackTarget(ReviewArea area) => area switch
    {
        ReviewArea.Knowledge => GenerationTarget.Knowledge,
        _ => null,
    };
}
