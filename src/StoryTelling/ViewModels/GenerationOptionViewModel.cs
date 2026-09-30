namespace StoryTelling.ViewModels;

public sealed class GenerationOptionViewModel
{
    public GenerationOptionViewModel(string display, IReadOnlyDictionary<string, string> fields)
    {
        Display = display;
        Fields = fields;
    }

    public string Display { get; }

    public IReadOnlyDictionary<string, string> Fields { get; }
}
