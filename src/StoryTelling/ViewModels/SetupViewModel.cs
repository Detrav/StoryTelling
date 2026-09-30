using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using StoryTelling.Application.Abstractions;
using StoryTelling.Application.Generation;
using StoryTelling.Application.Knowledge;
using StoryTelling.Application.Settings;
using StoryTelling.Domain;

namespace StoryTelling.ViewModels;

public partial class SetupViewModel : UndoableDialogViewModel
{
    private readonly IGenerationAssistant _assistant;
    private readonly IKnowledgeImporter _importer;

    public SetupViewModel(
        ITextDiff diff,
        IReadOnlyList<LanguageData> catalog,
        IEnumerable<string> selectedCodes,
        IGenerationAssistant assistant,
        IKnowledgeImporter importer)
        : base(diff)
    {
        _assistant = assistant;
        _importer = importer;
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
    private string _premise = string.Empty;

    [ObservableProperty]
    private string _direction = string.Empty;

    [ObservableProperty]
    private string _style = string.Empty;

    [ObservableProperty]
    private string _pointOfView = string.Empty;

    [ObservableProperty]
    private string _tense = string.Empty;

    [ObservableProperty]
    private string _rating = string.Empty;

    [ObservableProperty]
    private string _worldStateTimeAndPlace = string.Empty;

    [ObservableProperty]
    private string _worldStateDescription = string.Empty;

    public ObservableCollection<CharacterEditorViewModel> Characters { get; } = [];

    [ObservableProperty]
    private CharacterEditorViewModel? _selectedCharacter;

    public ObservableCollection<KnowledgeEntryEditorViewModel> Knowledge { get; } = [];

    [ObservableProperty]
    private KnowledgeEntryEditorViewModel? _selectedKnowledge;

    public string LabelFor(string field) => field switch
    {
        "ProjectName" => "Book name",
        "WorldTitle" => "World title",
        "WorldBody" => "World description",
        "Characters" => "Characters",
        "Premise" => "Premise",
        "WorldState" => "Initial world state",
        _ => field,
    };

    public Task<IReadOnlyList<GenerationOption>> GenerateAsync(GenerationTarget target, string brief, int options, GenerationSession session, IProgress<GenerationProgress>? progress, CancellationToken cancellationToken)
    {
        var request = new GenerationRequest
        {
            Target = target,
            Brief = brief,
            Variants = options,
            Context = new GenerationContext { Fields = ProjectFields(), Cast = BuildCast() },
            Snapshot = BuildSnapshot(),
        };

        return _assistant.GenerateAsync(request, session, progress, cancellationToken);
    }

    public Task<IReadOnlyList<GenerationOption>> GenerateCharacterAsync(CharacterEditorViewModel character, string brief, int options, GenerationSession session, IProgress<GenerationProgress>? progress, CancellationToken cancellationToken)
    {
        var fields = ProjectFields();
        foreach (var (key, value) in character.ToFields())
        {
            fields[key] = value;
        }

        var request = new GenerationRequest
        {
            Target = GenerationTarget.Character,
            Brief = brief,
            Variants = options,
            Context = new GenerationContext { Fields = fields, Cast = BuildCast(character) },
            Snapshot = BuildSnapshot(),
        };

        return _assistant.GenerateAsync(request, session, progress, cancellationToken);
    }

    private Project BuildSnapshot() => new()
    {
        Name = ProjectName,
        Frame = new StoryFrame
        {
            Genre = Genre,
            Tone = Tone,
            Style = Style,
            PointOfView = PointOfView,
            Tense = Tense,
            Rating = Rating,
            Premise = Premise,
            Direction = Direction,
        },
        Lore = new WorldLore { Title = WorldTitle, Body = WorldBody },
        Characters =
        [
            .. Characters.Where(character => !string.IsNullOrWhiteSpace(character.Name)).Select(character => new Character
            {
                Id = character.Id,
                Name = character.Name,
                Role = character.Role,
                Age = character.Age,
                Description = character.Description,
                Personality = character.Personality,
                Background = character.Background,
                Goals = character.Goals,
                Traits = [.. character.TraitList],
            }),
        ],
        Knowledge = [.. Knowledge.Select(entry => entry.ToEntry())],
        WorldState = new WorldState { TimeAndPlace = WorldStateTimeAndPlace, Description = WorldStateDescription },
    };

    private List<string> BuildCast(CharacterEditorViewModel? exclude = null)
    {
        var cast = new List<string>();
        foreach (var character in Characters)
        {
            if (string.IsNullOrWhiteSpace(character.Name) || ReferenceEquals(character, exclude))
            {
                continue;
            }

            var detail = !string.IsNullOrWhiteSpace(character.Role) ? character.Role : character.Description;
            cast.Add(string.IsNullOrWhiteSpace(detail) ? character.Name.Trim() : $"{character.Name.Trim()} — {detail.Trim()}");
        }

        return cast;
    }

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
        ["Premise"] = Premise,
        ["Direction"] = Direction,
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
            case "Premise":
                Premise = text;
                break;
            case "Genre":
                Genre = FirstLine(text);
                break;
            case "Tone":
                Tone = FirstLine(text);
                break;
            case "Direction":
                Direction = FirstLine(text);
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
                WorldStateTimeAndPlace = FirstLine(text);
                break;
            case "Description":
                WorldStateDescription = text;
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
            Premise,
            Direction,
            WorldStateTimeAndPlace,
            WorldStateDescription,
            [.. Characters.Select(character => new CharacterSnapshot(
                character.Id,
                character.Name,
                character.Role,
                character.Age,
                character.Description,
                character.Personality,
                character.Background,
                character.Goals,
                character.Traits))],
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
        Premise = snapshot.Premise;
        Direction = snapshot.Direction;
        WorldStateTimeAndPlace = snapshot.WorldStateTimeAndPlace;
        WorldStateDescription = snapshot.WorldStateDescription;

        ReplaceCharacters(snapshot.Characters);
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

    private void ReplaceCharacters(IReadOnlyList<CharacterSnapshot> values)
    {
        Characters.Clear();
        SelectedCharacter = null;
        foreach (var value in values)
        {
            Characters.Add(new CharacterEditorViewModel(
                value.Id,
                value.Name,
                value.Role,
                value.Age,
                value.Description,
                value.Personality,
                value.Background,
                value.Goals,
                value.Traits));
        }
    }

    public void AddKnowledge(KnowledgeEntryEditorViewModel entry)
    {
        Knowledge.Add(entry);
        SelectedKnowledge = entry;
        Commit();
    }

    public void AddKnowledgeRange(IEnumerable<KnowledgeEntryEditorViewModel> entries)
    {
        var added = false;
        foreach (var entry in entries)
        {
            Knowledge.Add(entry);
            added = true;
        }

        if (added)
        {
            SelectedKnowledge = Knowledge.LastOrDefault();
            Commit();
        }
    }

    public Task<IReadOnlyList<KnowledgeEntry>> ExtractKnowledgeAsync(string content, string brief, IProgress<int>? progress, CancellationToken cancellationToken) =>
        _importer.ExtractAsync(new KnowledgeImportRequest(content, brief), progress, cancellationToken);

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

    public void AddCharacter(CharacterEditorViewModel character)
    {
        Characters.Add(character);
        SelectedCharacter = character;
        Commit();
    }

    public void ApplyCharacterEdit(CharacterEditorViewModel target, CharacterEditorViewModel draft)
    {
        target.CopyFrom(draft);
        Commit();
    }

    public void RemoveCharacter(CharacterEditorViewModel character)
    {
        Characters.Remove(character);
        if (ReferenceEquals(SelectedCharacter, character))
        {
            SelectedCharacter = null;
        }

        Commit();
    }

    public static GenerationTarget MapTarget(string tag) => tag switch
    {
        "ProjectName" => GenerationTarget.ProjectName,
        "World" => GenerationTarget.World,
        "Frame" => GenerationTarget.Frame,
        "Premise" => GenerationTarget.Premise,
        "WorldState" => GenerationTarget.WorldState,
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
        string Premise,
        string Direction,
        string WorldStateTimeAndPlace,
        string WorldStateDescription,
        List<CharacterSnapshot> Characters,
        List<KnowledgeSnapshot> Knowledge,
        List<string> Languages);

    private sealed record KnowledgeSnapshot(Guid Id, KnowledgeKind Kind, string Title, string Tags, string Content);

    private sealed record CharacterSnapshot(
        Guid Id,
        string Name,
        string Role,
        string Age,
        string Description,
        string Personality,
        string Background,
        string Goals,
        string Traits);
}
