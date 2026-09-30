using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace StoryTelling.ViewModels;

public partial class SetupViewModel : ViewModelBase
{
    public SetupViewModel(LanguageCatalog catalog)
    {
        Catalog = catalog;
        LanguageSelections = new ObservableCollection<LanguageSelectionViewModel>(
            catalog.Items.Select(option => new LanguageSelectionViewModel(option, option.Code == catalog.DefaultCode)));
    }

    public LanguageCatalog Catalog { get; }

    public ObservableCollection<LanguageSelectionViewModel> LanguageSelections { get; }

    public IReadOnlyList<string> SelectedLanguageCodes =>
        LanguageSelections.Where(selection => selection.IsSelected).Select(selection => selection.Option.Code).ToList();

    [ObservableProperty]
    private string _projectName = "The Ember Crown";

    [ObservableProperty]
    private string _worldTitle = "Ashen Reach";

    [ObservableProperty]
    private string _worldBody =
        "A dying empire under a pale sun. Trade roads rot, the Ember Crown weakens, and old oaths are being called in.";

    [ObservableProperty]
    private string _genre = "Fantasy";

    [ObservableProperty]
    private string _tone = "Grim, hopeful";

    [ObservableProperty]
    private string _premise = "A frontier scout is drawn into a rebellion she never wanted.";

    [ObservableProperty]
    private string _direction = "Rise, fracture, resolve.";

    [ObservableProperty]
    private int _chapterCount = 5;

    [ObservableProperty]
    private string _worldStateTimeAndPlace = "Dusk, day 0 — the story begins at the burning keep.";

    public ObservableCollection<string> Characters { get; } =
    [
        "Aria — frontier scout, searching for her brother",
        "Bran — smuggler with a debt he cannot pay",
        "The Ember Queen — ruler losing her grip",
    ];

    public ObservableCollection<string> ExtraFiles { get; } =
    [
        "bestiary.md",
        "ember-geography.txt",
    ];

    public ObservableCollection<string> WorldStateCharacters { get; } =
    [
        "Aria — at the keep, unaware of the relic",
        "Bran — in the low city, owed a debt",
    ];

    public ObservableCollection<string> WorldStateThreads { get; } =
    [
        "The Ember Crown weakens",
    ];

    public ObservableCollection<string> WorldStateItems { get; } =
    [
        "The relic (hidden)",
    ];

    public ObservableCollection<string> WorldStateOpenQuestions { get; } =
    [
        "Why did the keep fall?",
    ];

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

    public void ApplyGenerated(string field, string text)
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
                ExtraFiles.Add("generated-notes.md");
                break;
            case "WorldState":
                WorldStateTimeAndPlace = FirstLine(text);
                break;
        }
    }

    private static string FirstLine(string text)
    {
        var index = text.IndexOf('\n');
        return index < 0 ? text : text[..index].Trim();
    }
}
