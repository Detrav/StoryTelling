using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using StoryTelling.Application.Settings;

namespace StoryTelling.ViewModels;

public partial class SetupViewModel : ViewModelBase
{
    public SetupViewModel(IReadOnlyList<LanguageData> catalog, IEnumerable<string> selectedCodes)
    {
        var selected = selectedCodes.ToList();

        LanguageSelections = new ObservableCollection<LanguageSelectionViewModel>(
            catalog.Select(language => new LanguageSelectionViewModel(language, selected.Contains(language.Code))));
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
    private int _chapterCount = 1;

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
