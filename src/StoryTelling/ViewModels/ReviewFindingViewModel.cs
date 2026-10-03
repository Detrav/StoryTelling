using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StoryTelling.Application.Generation;
using StoryTelling.Application.Llm;
using StoryTelling.Application.Review;
using StoryTelling.Domain;

namespace StoryTelling.ViewModels;

public partial class ReviewFindingViewModel : ObservableObject
{
    private static readonly string[] _creationMarkers =
    [
        "no dedicated", "no entry", "has no entry", "no knowledge entry", "missing entry",
        "dangling reference", "undefined reference", "does not exist", "never defined",
        "no character entry", "no such entry", "is referenced but", "is mentioned but",
    ];

    private readonly IReviewFixHost? _host;

    public ReviewFindingViewModel(ReviewFinding finding, IReviewFixHost? host = null, bool canFixWithAi = true)
    {
        Finding = finding;
        _host = host;
        Action = ClassifyAction(finding, canFixWithAi);

        if (ExplicitTarget(finding.Area, finding.Reference) is { } target)
        {
            AiTarget = target;
            AiReference = finding.Reference ?? string.Empty;
        }
        else if (host is not null
            && FallbackTarget(finding.Area) is { } fallback
            && host.SingleReference(fallback) is { Length: > 0 } reference)
        {
            AiTarget = fallback;
            AiReference = reference;
        }
    }

    public event EventHandler? Applied;

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

    public ObservableCollection<ReviewEditRowViewModel> Rows { get; } = [];

    [ObservableProperty]
    private bool _isSelected = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanFix))]
    [NotifyPropertyChangedFor(nameof(FixLabel))]
    private bool _isApplied;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanFix))]
    [NotifyPropertyChangedFor(nameof(FixLabel))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanFix))]
    [NotifyPropertyChangedFor(nameof(FixLabel))]
    [NotifyPropertyChangedFor(nameof(HasSelectedRows))]
    private bool _isPrepared;

    [ObservableProperty]
    private string _status = string.Empty;

    partial void OnStatusChanged(string value) => OnPropertyChanged(nameof(HasStatus));

    public bool HasStatus => !string.IsNullOrWhiteSpace(Status);

    public bool HasSelectedRows => Rows.Any(row => row.IsSelected);

    public bool CanFix => !IsApplied && !IsBusy && Action != ReviewFindingAction.None;

    public string FixLabel => IsApplied
        ? "Applied"
        : IsPrepared
            ? "Apply"
            : Action == ReviewFindingAction.FixWithAi
                ? "Generate"
                : "Prepare";

    public async Task PrepareAsync(CancellationToken cancellationToken = default)
    {
        if (_host is null || IsPrepared || IsApplied || IsBusy || Action == ReviewFindingAction.None)
        {
            return;
        }

        IsBusy = true;
        Status = "Preparing…";

        try
        {
            switch (Action)
            {
                case ReviewFindingAction.Fix:
                    BuildRows(_host.PreviewFix(Finding.Fix!));
                    break;
                case ReviewFindingAction.CreateEntry:
                    BuildCreateRows();
                    break;
                case ReviewFindingAction.FixWithAi:
                    await BuildAiRowsAsync(cancellationToken);
                    break;
            }

            IsPrepared = Rows.Count > 0;
            if (IsPrepared)
            {
                Status = $"{Rows.Count} change(s).";
            }
            else if (Status == "Preparing…")
            {
                Status = "Nothing to apply.";
            }
        }
        catch (OperationCanceledException)
        {
            Status = "Stopped.";
        }
        catch (LlmException exception)
        {
            Status = $"Failed ({exception.Kind}): {exception.Message}";
        }
        catch (Exception exception)
        {
            Status = $"Failed: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task FixAsync()
    {
        if (IsApplied || _host is null || Action == ReviewFindingAction.None)
        {
            return;
        }

        if (!IsPrepared)
        {
            await PrepareAsync();
            return;
        }

        await ApplyAsync();
    }

    public Task ApplyAsync()
    {
        if (_host is null || !IsPrepared || IsApplied || IsBusy)
        {
            return Task.CompletedTask;
        }

        if (BuildFix() is not { } fix)
        {
            Status = "Select at least one change.";
            return Task.CompletedTask;
        }

        _host.ApplyFix(fix, $"Fix: {Title}");
        IsApplied = true;
        Status = "Applied.";
        Applied?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public ReviewFix? BuildFix()
    {
        var edits = Rows
            .Where(row => row.IsSelected && row.Edit is not null)
            .Select(row => row.ToEdit()!)
            .ToList();
        return edits.Count == 0 ? null : new ReviewFix(edits);
    }

    private void BuildRows(IReadOnlyList<ReviewChange> changes)
    {
        foreach (var change in changes)
        {
            AddRow(new ReviewEditRowViewModel(change.Edit.Field, change.Label, change.OldValue, change.NewValue, change.Edit));
        }
    }

    private void BuildCreateRows()
    {
        var title = GuessTitle();
        if (string.IsNullOrWhiteSpace(title))
        {
            title = "New entry";
        }

        var edit = new ReviewEdit(GenerationTarget.Knowledge, title.Trim(), "Entry", Detail, ReviewEditOperation.Create, GuessKind(), null);
        BuildRows(_host!.PreviewFix(new ReviewFix([edit])));
    }

    private async Task BuildAiRowsAsync(CancellationToken cancellationToken)
    {
        var targets = _host!.FixTargets();
        var target = AiTarget ?? targets.FirstOrDefault()?.Target;
        if (target is null || targets.Count == 0)
        {
            Status = "There is no entry this fix could be applied to.";
            return;
        }

        var reference = ResolveReference(targets);
        if (reference.Length == 0)
        {
            Status = "There is no entry this fix could be applied to.";
            return;
        }

        var brief = string.Join("\n\n", new[]
        {
            $"Rewrite the knowledge entry \"{reference}\" so it resolves this review finding.",
            Detail,
            Suggestion,
        }.Where(text => !string.IsNullOrWhiteSpace(text)));
        var options = await _host.ProposeEntryAsync(reference, brief, cancellationToken);
        var fields = options.FirstOrDefault()?.Fields;
        if (fields is null || fields.Count == 0)
        {
            Status = "The AI returned no suggestion. Try again, or edit the entry manually.";
            return;
        }

        var edits = GenerationTargets.Fields(target.Value)
            .Where(spec => fields.TryGetValue(spec.Field, out var value) && !string.IsNullOrWhiteSpace(value))
            .Select(spec => new ReviewEdit(target.Value, reference, spec.Field, fields[spec.Field]))
            .ToList();

        if (edits.Count > 0)
        {
            BuildRows(_host.PreviewFix(new ReviewFix(edits)));
        }

        if (Rows.Count == 0)
        {
            Status = "The suggestion could not be matched to an entry.";
        }
    }

    private string ResolveReference(IReadOnlyList<ReviewFixTarget> targets)
    {
        var trimmed = AiReference.Trim();
        if (trimmed.Length == 0)
        {
            return targets[0].Reference;
        }

        var exact = targets.FirstOrDefault(target => string.Equals(target.Reference, trimmed, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact.Reference;
        }

        var partial = targets
            .Where(target => !string.IsNullOrWhiteSpace(target.Reference)
                && (target.Reference.Contains(trimmed, StringComparison.OrdinalIgnoreCase)
                    || trimmed.Contains(target.Reference, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        return partial.Count == 1 ? partial[0].Reference : trimmed;
    }

    private void AddRow(ReviewEditRowViewModel row)
    {
        row.PropertyChanged += OnRowChanged;
        Rows.Add(row);
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(ReviewEditRowViewModel.IsSelected) or nameof(ReviewEditRowViewModel.NewValue))
        {
            OnPropertyChanged(nameof(HasSelectedRows));
        }
    }

    private string GuessTitle()
    {
        var title = Finding.Reference;
        if (!string.IsNullOrWhiteSpace(title))
        {
            return title.Trim();
        }

        var cleaned = Title
            .Replace("Missing central character entry for", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("Missing entry for", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("Dangling reference to", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("Missing entry:", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim(' ', ':', '-', '.');
        return cleaned.Length > 0 ? cleaned : Title;
    }

    private KnowledgeKind GuessKind()
    {
        var text = $"{Title} {Detail}".ToLowerInvariant();
        if (text.Contains("character") || text.Contains("captain") || text.Contains("surname") || text.Contains("protagonist"))
        {
            return KnowledgeKind.Character;
        }

        if (text.Contains("place") || text.Contains("location") || text.Contains("district"))
        {
            return KnowledgeKind.Place;
        }

        if (text.Contains("group") || text.Contains("syndicate") || text.Contains("organization") || text.Contains("faction"))
        {
            return KnowledgeKind.Faction;
        }

        return KnowledgeKind.Background;
    }

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
