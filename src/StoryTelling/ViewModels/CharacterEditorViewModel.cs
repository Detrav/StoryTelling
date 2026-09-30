using CommunityToolkit.Mvvm.ComponentModel;
using StoryTelling.Application.Generation;

namespace StoryTelling.ViewModels;

public partial class CharacterEditorViewModel : ObservableObject
{
    public CharacterEditorViewModel()
    {
    }

    public CharacterEditorViewModel(
        Guid id,
        string name,
        string role,
        string age,
        string description,
        string personality,
        string background,
        string goals,
        IEnumerable<string> traits)
    {
        Id = id;
        _name = name;
        _role = role;
        _age = age;
        _description = description;
        _personality = personality;
        _background = background;
        _goals = goals;
        _traits = string.Join(", ", traits);
    }

    public CharacterEditorViewModel(
        Guid id,
        string name,
        string role,
        string age,
        string description,
        string personality,
        string background,
        string goals,
        string traits)
    {
        Id = id;
        _name = name;
        _role = role;
        _age = age;
        _description = description;
        _personality = personality;
        _background = background;
        _goals = goals;
        _traits = traits;
    }

    public Guid Id { get; init; } = Guid.NewGuid();

    public Func<string, int, CancellationToken, Task<IReadOnlyList<GenerationOption>>>? GenerateOptions { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasName))]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _role = string.Empty;

    [ObservableProperty]
    private string _age = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _personality = string.Empty;

    [ObservableProperty]
    private string _background = string.Empty;

    [ObservableProperty]
    private string _goals = string.Empty;

    [ObservableProperty]
    private string _traits = string.Empty;

    public bool HasName => !string.IsNullOrWhiteSpace(Name);

    public IReadOnlyList<string> TraitList =>
        Traits.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public IReadOnlyDictionary<string, string> ToFields() => new Dictionary<string, string>
    {
        ["Name"] = Name,
        ["Role"] = Role,
        ["Age"] = Age,
        ["Description"] = Description,
        ["Personality"] = Personality,
        ["Background"] = Background,
        ["Goals"] = Goals,
        ["Traits"] = Traits,
    };

    public void ApplyFields(IReadOnlyDictionary<string, string> fields)
    {
        Name = Get(fields, "Name", Name);
        Role = Get(fields, "Role", Role);
        Age = Get(fields, "Age", Age);
        Description = Get(fields, "Description", Description);
        Personality = Get(fields, "Personality", Personality);
        Background = Get(fields, "Background", Background);
        Goals = Get(fields, "Goals", Goals);
        Traits = Get(fields, "Traits", Traits);
    }

    public CharacterEditorViewModel Clone() =>
        new(Id, Name, Role, Age, Description, Personality, Background, Goals, TraitList);

    public void CopyFrom(CharacterEditorViewModel other)
    {
        Name = other.Name;
        Role = other.Role;
        Age = other.Age;
        Description = other.Description;
        Personality = other.Personality;
        Background = other.Background;
        Goals = other.Goals;
        Traits = other.Traits;
    }

    private static string Get(IReadOnlyDictionary<string, string> fields, string key, string fallback) =>
        fields.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : fallback;
}
