using CommunityToolkit.Mvvm.ComponentModel;
using StoryTelling.Application.Generation;
using StoryTelling.Application.Review;

namespace StoryTelling.ViewModels;

public partial class ReviewFindingViewModel : ObservableObject
{
    public ReviewFindingViewModel(ReviewFinding finding, Func<GenerationTarget, string?>? singleReference = null, bool canFixWithAi = true)
    {
        Finding = finding;
        _canFixWithAi = canFixWithAi;

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

    private readonly bool _canFixWithAi;

    public string Severity => Finding.Severity.ToString();

    public string Area => Finding.Area.ToString();

    public string Title => Finding.Title;

    public string Detail => Finding.Detail;

    public string? Suggestion => Finding.Suggestion;

    public bool HasSuggestion => !string.IsNullOrWhiteSpace(Finding.Suggestion);

    public bool CanApplyFix => Finding.Fix is { IsEmpty: false };

    public GenerationTarget? AiTarget { get; }

    public string AiReference { get; } = string.Empty;

    public bool CanFixWithAi => _canFixWithAi;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FixLabel))]
    private bool _isFixed;

    public string FixLabel => IsFixed ? "Fixed" : "Fix";

    private static GenerationTarget? ExplicitTarget(ReviewArea area, string? reference) => area switch
    {
        ReviewArea.Frame => GenerationTarget.Frame,
        ReviewArea.World => GenerationTarget.World,
        ReviewArea.WorldState => GenerationTarget.WorldState,
        ReviewArea.Characters => string.IsNullOrWhiteSpace(reference) ? null : GenerationTarget.Character,
        ReviewArea.Knowledge => string.IsNullOrWhiteSpace(reference) ? null : GenerationTarget.Knowledge,
        _ => null,
    };

    private static GenerationTarget? FallbackTarget(ReviewArea area) => area switch
    {
        ReviewArea.Characters => GenerationTarget.Character,
        ReviewArea.Knowledge => GenerationTarget.Knowledge,
        _ => null,
    };
}
