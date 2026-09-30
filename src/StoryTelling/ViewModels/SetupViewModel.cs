using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using StoryTelling.Application.Abstractions;
using StoryTelling.Application.Generation;
using StoryTelling.Application.Settings;

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

    public ObservableCollection<string> Characters { get; } = [];

    public ObservableCollection<string> ExtraFiles { get; } = [];

    public ObservableCollection<string> WorldStateCharacters { get; } = [];

    public ObservableCollection<string> WorldStateThreads { get; } = [];

    public ObservableCollection<string> WorldStateItems { get; } = [];

    public ObservableCollection<string> WorldStateOpenQuestions { get; } = [];

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

    public Task<IReadOnlyList<GenerationOption>> GenerateAsync(GenerationTarget target, string brief, CancellationToken cancellationToken)
    {
        var request = new GenerationRequest
        {
            Target = target,
            Brief = brief,
            Context = new GenerationContext
            {
                ProjectName = ProjectName,
                WorldTitle = WorldTitle,
                WorldBody = WorldBody,
                Genre = Genre,
                Tone = Tone,
                Premise = Premise,
                Direction = Direction,
            },
        };

        return _assistant.GenerateAsync(request, cancellationToken);
    }

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
            case "Characters":
                Characters.Clear();
                Characters.Add(text);
                break;
            case "ExtraFiles":
                ExtraFiles.Add(FirstLine(text));
                break;
            case "WorldState":
                WorldStateTimeAndPlace = FirstLine(text);
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
            [.. Characters],
            [.. ExtraFiles],
            [.. WorldStateCharacters],
            [.. WorldStateThreads],
            [.. WorldStateItems],
            [.. WorldStateOpenQuestions],
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

        Replace(Characters, snapshot.Characters);
        Replace(ExtraFiles, snapshot.ExtraFiles);
        Replace(WorldStateCharacters, snapshot.WorldStateCharacters);
        Replace(WorldStateThreads, snapshot.WorldStateThreads);
        Replace(WorldStateItems, snapshot.WorldStateItems);
        Replace(WorldStateOpenQuestions, snapshot.WorldStateOpenQuestions);

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

    public static GenerationTarget MapTarget(string tag) => tag switch
    {
        "ProjectName" => GenerationTarget.ProjectName,
        "World" => GenerationTarget.World,
        "Premise" => GenerationTarget.Premise,
        "Characters" => GenerationTarget.Characters,
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
        List<string> Characters,
        List<string> ExtraFiles,
        List<string> WorldStateCharacters,
        List<string> WorldStateThreads,
        List<string> WorldStateItems,
        List<string> WorldStateOpenQuestions,
        List<string> Languages);
}
