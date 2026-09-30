namespace StoryTelling.Application.Settings;

public sealed class LanguageData
{
    public LanguageData()
    {
    }

    public LanguageData(string code, string name)
    {
        Code = code;
        Name = name;
    }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string DisplayName => $"{Name} ({Code.ToUpperInvariant()})";
}
