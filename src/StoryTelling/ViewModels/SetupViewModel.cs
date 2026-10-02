using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using StoryTelling.Application.Abstractions;
using StoryTelling.Application.Generation;
using StoryTelling.Application.Knowledge;
using StoryTelling.Application.Review;
using StoryTelling.Application.Settings;
using StoryTelling.Domain;

namespace StoryTelling.ViewModels;

public partial class SetupViewModel : UndoableDialogViewModel, IReviewFixHost
{
    private readonly IGenerationAssistant _assistant;
    private readonly IKnowledgeImporter _importer;
    private readonly IProjectReviewAssistant _review;

    public SetupViewModel(
        ITextDiff diff,
        IReadOnlyList<LanguageData> catalog,
        IEnumerable<string> selectedCodes,
        IGenerationAssistant assistant,
        IKnowledgeImporter importer,
        IProjectReviewAssistant review)
        : base(diff)
    {
        _assistant = assistant;
        _importer = importer;
        _review = review;
        var selected = selectedCodes.ToList();

        LanguageSelections = new ObservableCollection<LanguageSelectionViewModel>(
            catalog.Select(language => new LanguageSelectionViewModel(language, selected.Contains(language.Code))));

        InitializeUndo();
    }

    public ObservableCollection<LanguageSelectionViewModel> LanguageSelections { get; }

    public IReadOnlyList<string> SelectedLanguageCodes =>
        LanguageSelections.Where(selection => selection.IsSelected).Select(selection => selection.Language.Code).ToList();

    [ObservableProperty]
    private string _projectName = string.Empty;

    [ObservableProperty]
    private string _worldTitle = string.Empty;

    [ObservableProperty]
    private string _worldBody = string.Empty;

    [ObservableProperty]
    private string _genre = string.Empty;

    [ObservableProperty]
    private string _tone = string.Empty;

    [ObservableProperty]
    private string _style = string.Empty;

    [ObservableProperty]
    private string _pointOfView = string.Empty;

    [ObservableProperty]
    private string _tense = string.Empty;

    [ObservableProperty]
    private string _rating = string.Empty;

    [ObservableProperty]
    private string _initialStateTimeAndPlace = string.Empty;

    [ObservableProperty]
    private string _initialStateDescription = string.Empty;

    public ObservableCollection<KnowledgeEntryEditorViewModel> Knowledge { get; } = [];

    [ObservableProperty]
    private KnowledgeEntryEditorViewModel? _selectedKnowledge;

    public string LabelFor(string field) => field switch
    {
        "ProjectName" => "Book name",
        "WorldTitle" => "World title",
        "WorldBody" => "World description",
        "World" => "World",
        "InitialWorldState" => "Initial world state",
        _ => field,
    };

    public Task<IReadOnlyList<GenerationOption>> GenerateAsync(GenerationTarget target, string brief, int options, GenerationSession session, IProgress<GenerationProgress>? progress, CancellationToken cancellationToken)
    {
        var request = new GenerationRequest
        {
            Target = target,
            Brief = brief,
            Variants = options,
            Context = new GenerationContext { Fields = ProjectFields() },
            Snapshot = BuildSnapshot(),
        };

        return _assistant.GenerateAsync(request, session, progress, cancellationToken);
    }

    public Task<IReadOnlyList<GenerationOption>> GenerateKnowledgeAsync(KnowledgeEntryEditorViewModel entry, string brief, int options, GenerationSession session, IProgress<GenerationProgress>? progress, CancellationToken cancellationToken)
    {
        var fields = ProjectFields();
        foreach (var (key, value) in entry.ToFields())
        {
            fields[key] = value;
        }

        var request = new GenerationRequest
        {
            Target = GenerationTarget.Knowledge,
            Brief = brief,
            Variants = options,
            Context = new GenerationContext { Fields = fields },
            Snapshot = BuildSnapshot(),
            Avoid =
            [
                .. Knowledge
                    .Select(existing => existing.Title.Trim())
                    .Where(title => title.Length > 0 && !string.Equals(title, entry.Title.Trim(), StringComparison.OrdinalIgnoreCase)),
            ],
        };

        return _assistant.GenerateAsync(request, session, progress, cancellationToken);
    }

    private Project BuildSnapshot() => new()
    {
        Name = ProjectName,
        World = new World
        {
            Title = WorldTitle,
            Body = WorldBody,
            Genre = Genre,
            Tone = Tone,
            Style = Style,
            PointOfView = PointOfView,
            Tense = Tense,
            Rating = Rating,
        },
        Knowledge = [.. Knowledge.Select(entry => entry.ToEntry())],
        InitialWorldState = new WorldState { TimeAndPlace = InitialStateTimeAndPlace, Situation = InitialStateDescription },
    };

    private Dictionary<string, string> ProjectFields() => new()
    {
        ["ProjectName"] = ProjectName,
        ["WorldTitle"] = WorldTitle,
        ["WorldBody"] = WorldBody,
        ["Genre"] = Genre,
        ["Tone"] = Tone,
        ["Style"] = Style,
        ["PointOfView"] = PointOfView,
        ["Tense"] = Tense,
        ["Rating"] = Rating,
    };

    public void ApplyGenerated(IReadOnlyDictionary<string, string> fields)
    {
        foreach (var (field, text) in fields)
        {
            ApplyField(field, text);
        }

        Commit();
    }

    private void ApplyField(string field, string text)
    {
        switch (field)
        {
            case "ProjectName":
                ProjectName = FirstLine(text);
                break;
            case "WorldTitle":
                WorldTitle = FirstLine(text);
                break;
            case "WorldBody":
                WorldBody = text;
                break;
            case "Genre":
                Genre = FirstLine(text);
                break;
            case "Tone":
                Tone = FirstLine(text);
                break;
            case "Style":
                Style = text;
                break;
            case "PointOfView":
                PointOfView = FirstLine(text);
                break;
            case "Tense":
                Tense = FirstLine(text);
                break;
            case "Rating":
                Rating = FirstLine(text);
                break;
            case "TimeAndPlace":
                InitialStateTimeAndPlace = FirstLine(text);
                break;
            case "Description":
                InitialStateDescription = text;
                break;
        }
    }

    protected override string CaptureState()
    {
        var snapshot = new SetupSnapshot(
            ProjectName,
            WorldTitle,
            WorldBody,
            Genre,
            Tone,
            Style,
            PointOfView,
            Tense,
            Rating,
            InitialStateTimeAndPlace,
            InitialStateDescription,
            [.. Knowledge.Select(entry => new KnowledgeSnapshot(entry.Id, entry.Kind, entry.Title, entry.Tags, entry.Content))],
            [.. SelectedLanguageCodes]);

        return JsonSerializer.Serialize(snapshot);
    }

    protected override void ApplyState(string state)
    {
        var snapshot = JsonSerializer.Deserialize<SetupSnapshot>(state);
        if (snapshot is null)
        {
            return;
        }

        ProjectName = snapshot.ProjectName;
        WorldTitle = snapshot.WorldTitle;
        WorldBody = snapshot.WorldBody;
        Genre = snapshot.Genre;
        Tone = snapshot.Tone;
        Style = snapshot.Style;
        PointOfView = snapshot.PointOfView;
        Tense = snapshot.Tense;
        Rating = snapshot.Rating;
        InitialStateTimeAndPlace = snapshot.InitialStateTimeAndPlace;
        InitialStateDescription = snapshot.InitialStateDescription;

        ReplaceKnowledge(snapshot.Knowledge);

        foreach (var selection in LanguageSelections)
        {
            selection.IsSelected = snapshot.Languages.Contains(selection.Language.Code);
        }
    }

    private void ReplaceKnowledge(IReadOnlyList<KnowledgeSnapshot> values)
    {
        Knowledge.Clear();
        SelectedKnowledge = null;
        foreach (var value in values)
        {
            Knowledge.Add(new KnowledgeEntryEditorViewModel
            {
                Id = value.Id,
                Kind = value.Kind,
                Title = value.Title,
                Tags = value.Tags,
                Content = value.Content,
            });
        }
    }

    public void AddKnowledge(KnowledgeEntryEditorViewModel entry)
    {
        Knowledge.Add(entry);
        SelectedKnowledge = entry;
        Commit();
    }

    public IReadOnlyList<string> KnowledgeTitles =>
        [.. Knowledge.Select(entry => entry.Title.Trim()).Where(title => title.Length > 0)];

    public void AddKnowledgeRange(IEnumerable<KnowledgeEntryEditorViewModel> entries)
    {
        var existing = new HashSet<string>(KnowledgeTitles, StringComparer.OrdinalIgnoreCase);
        var added = false;
        foreach (var entry in entries)
        {
            if (!string.IsNullOrWhiteSpace(entry.Title) && !existing.Add(entry.Title.Trim()))
            {
                continue;
            }

            Knowledge.Add(entry);
            added = true;
        }

        if (added)
        {
            SelectedKnowledge = Knowledge.LastOrDefault();
            Commit();
        }
    }

    public Task<IReadOnlyList<KnowledgeEntry>> ExtractKnowledgeAsync(string content, string brief, IProgress<KnowledgeImportProgress>? progress, CancellationToken cancellationToken) =>
        _importer.ExtractAsync(new KnowledgeImportRequest(content, brief), progress, cancellationToken);

    public Task<IReadOnlyList<KnowledgeEntry>> DesignKnowledgeAsync(string description, string brief, IProgress<KnowledgeImportProgress>? progress, CancellationToken cancellationToken) =>
        _importer.ExtractAsync(new KnowledgeImportRequest(description, brief, Mode: KnowledgeImportMode.Design), progress, cancellationToken);

    public KnowledgeImportPlan PlanKnowledgeImport(string content) => _importer.Plan(content);

    public Task<IReadOnlyList<ReviewFinding>> RunReviewAsync(ReviewCheck check, string brief, IProgress<GenerationProgress>? progress, CancellationToken cancellationToken) =>
        _review.ReviewAsync(BuildSnapshot(), brief, check, progress, cancellationToken);

    public string ReviewSignature()
    {
        var snapshot = BuildSnapshot();
        var parts = new List<string>
        {
            snapshot.Name,
            snapshot.World.Title,
            snapshot.World.Body,
        };
        parts.AddRange(snapshot.Knowledge.Select(entry => $"{entry.Kind}|{entry.Title}|{entry.Content}"));
        return string.Join('\n', parts);
    }

    public IReadOnlyList<ReviewChange> PreviewFix(ReviewFix fix) => ResolveFix(fix, apply: false);

    public string? SingleReference(GenerationTarget target) => target switch
    {
        GenerationTarget.Knowledge => Single(Knowledge.Select(entry => entry.Title.Trim()).Where(title => title.Length > 0)),
        _ => null,
    };

    private static string? Single(IEnumerable<string> values)
    {
        var found = values.Take(2).ToList();
        return found.Count == 1 ? found[0] : null;
    }

    public IReadOnlyList<ReviewFixTarget> FixTargets() =>
        [.. Knowledge
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Title))
            .Select(entry => new ReviewFixTarget(GenerationTarget.Knowledge, entry.Title.Trim(), $"Knowledge: {entry.Title.Trim()}"))];

    public void ApplyFix(ReviewFix fix, string label)
    {
        if (ResolveFix(fix, apply: true).Count > 0)
        {
            PushUndo(label);
        }
    }

    public void AddEntry(KnowledgeEntry entry, string label)
    {
        Knowledge.Add(new KnowledgeEntryEditorViewModel(entry));
        SelectedKnowledge = Knowledge.LastOrDefault();
        PushUndo(label);
    }

    private List<ReviewChange> ResolveFix(ReviewFix fix, bool apply)
    {
        var changes = new List<ReviewChange>();
        foreach (var edit in fix.Edits)
        {
            if (!TryResolveEdit(edit, out var label, out var current, out var set))
            {
                continue;
            }

            if (apply)
            {
                set(edit.Value);
            }

            changes.Add(new ReviewChange(label, current, edit.Value));
        }

        return changes;
    }

    private bool TryResolveEdit(ReviewEdit edit, out string label, out string current, out Action<string> set)
    {
        label = string.Empty;
        current = string.Empty;
        set = _ => { };

        if (GenerationTargets.FindField(edit.Target, edit.Field) is not { } spec)
        {
            return false;
        }

        if (edit.Target != GenerationTarget.Knowledge)
        {
            return false;
        }

        var entry = FindKnowledge(edit.Reference);
        if (entry is null || !entry.ToFields().TryGetValue(edit.Field, out var entryValue))
        {
            return false;
        }

        current = entryValue!;
        label = $"{entry.Title} · {spec.Label}";
        set = value => entry.ApplyFields(new Dictionary<string, string> { [edit.Field] = value });
        return true;
    }

    private KnowledgeEntryEditorViewModel? FindKnowledge(string reference) =>
        string.IsNullOrWhiteSpace(reference)
            ? null
            : Knowledge.FirstOrDefault(entry => string.Equals(entry.Title.Trim(), reference.Trim(), StringComparison.OrdinalIgnoreCase));

    public void ApplyKnowledgeEdit(KnowledgeEntryEditorViewModel target, KnowledgeEntryEditorViewModel draft)
    {
        target.CopyFrom(draft);
        Commit();
    }

    public void RemoveKnowledge(KnowledgeEntryEditorViewModel entry)
    {
        Knowledge.Remove(entry);
        if (ReferenceEquals(SelectedKnowledge, entry))
        {
            SelectedKnowledge = null;
        }

        Commit();
    }

    public static GenerationTarget MapTarget(string tag) => tag switch
    {
        "ProjectName" => GenerationTarget.ProjectName,
        "World" => GenerationTarget.World,
        "InitialWorldState" => GenerationTarget.InitialWorldState,
        "Knowledge" => GenerationTarget.Knowledge,
        _ => GenerationTarget.World,
    };

    private static string FirstLine(string text)
    {
        var index = text.IndexOf('\n');
        return index < 0 ? text : text[..index].Trim();
    }

    private sealed record SetupSnapshot(
        string ProjectName,
        string WorldTitle,
        string WorldBody,
        string Genre,
        string Tone,
        string Style,
        string PointOfView,
        string Tense,
        string Rating,
        string InitialStateTimeAndPlace,
        string InitialStateDescription,
        List<KnowledgeSnapshot> Knowledge,
        List<string> Languages);

    private sealed record KnowledgeSnapshot(Guid Id, KnowledgeKind Kind, string Title, string Tags, string Content);
}
