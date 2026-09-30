using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using StoryTelling.Application.Abstractions;
using StoryTelling.Application.Generation;
using StoryTelling.Application.Settings;
using StoryTelling.Domain;

namespace StoryTelling.ViewModels;

public partial class SetupViewModel : UndoableDialogViewModel
{
    private readonly IGenerationAssistant _assistant;

    public SetupViewModel(
        ITextDiff diff,
        IReadOnlyList<LanguageData> catalog,
        IEnumerable<string> selectedCodes,
        IGenerationAssistant assistant)
        : base(diff)
    {
        _assistant = assistant;
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
    private string _worldStateTimeAndPlace = string.Empty;

    [ObservableProperty]
    private string _worldStateDescription = string.Empty;

    public ObservableCollection<CharacterEditorViewModel> Characters { get; } = [];

    [ObservableProperty]
    private CharacterEditorViewModel? _selectedCharacter;

    public ObservableCollection<string> ExtraFiles { get; } = [];

    public string LabelFor(string field) => field switch
    {
        "ProjectName" => "Book name",
        "WorldTitle" => "World title",
        "WorldBody" => "World description",
        "Characters" => "Characters",
        "Premise" => "Premise",
        "ExtraFiles" => "Extra file",
        "WorldState" => "Initial world state",
        _ => field,
    };

    public Task<IReadOnlyList<GenerationOption>> GenerateAsync(GenerationTarget target, string brief, int options, CancellationToken cancellationToken)
    {
        var request = new GenerationRequest
        {
            Target = target,
            Brief = brief,
            Variants = options,
            Context = new GenerationContext { Fields = ProjectFields(), Cast = BuildCast() },
        };

        return _assistant.GenerateAsync(request, cancellationToken);
    }

    public Task<IReadOnlyList<GenerationOption>> GenerateCharacterAsync(CharacterEditorViewModel character, string brief, int options, CancellationToken cancellationToken)
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
        };

        return _assistant.GenerateAsync(request, cancellationToken);
    }

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
            case "ExtraFiles":
                ExtraFiles.Add(FirstLine(text));
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
            [.. ExtraFiles],
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
        Premise = snapshot.Premise;
        Direction = snapshot.Direction;
        WorldStateTimeAndPlace = snapshot.WorldStateTimeAndPlace;
        WorldStateDescription = snapshot.WorldStateDescription;

        ReplaceCharacters(snapshot.Characters);
        Replace(ExtraFiles, snapshot.ExtraFiles);

        foreach (var selection in LanguageSelections)
        {
            selection.IsSelected = snapshot.Languages.Contains(selection.Language.Code);
        }
    }

    private static void Replace(ObservableCollection<string> target, IReadOnlyList<string> values)
    {
        target.Clear();
        foreach (var value in values)
        {
            target.Add(value);
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
        "Plot" => GenerationTarget.Plot,
        "Premise" => GenerationTarget.Premise,
        "ExtraFiles" => GenerationTarget.ExtraFiles,
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
        string Premise,
        string Direction,
        string WorldStateTimeAndPlace,
        string WorldStateDescription,
        List<CharacterSnapshot> Characters,
        List<string> ExtraFiles,
        List<string> Languages);

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
