namespace StoryTelling.Application.Settings;

public sealed class AppSettings
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public string Provider { get; set; } = "OpenAI";

    public string BaseUrl { get; set; } = "https://api.openai.com/v1";

    public string Model { get; set; } = "gpt-4o-mini";

    public string ApiKey { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 120;

    public int MaxTokens { get; set; } = 16384;

    public int MaxToolCalls { get; set; } = 12;

    public int ContextTokenBudget { get; set; } = 4000;

    public int RecentLoglineCount { get; set; } = 3;

    public int ContextRequiredSectionMaxChars { get; set; } = 6000;

    public int ToolResultMaxChars { get; set; } = 24000;

    public double Temperature { get; set; } = 0.8;

    public string DefaultLanguageCode { get; set; } = "ru";

    public List<LanguageData> Languages { get; set; } = [];

    public List<string> RecentProjects { get; set; } = [];

    public static AppSettings CreateDefault() => new()
    {
        Languages =
        [
            new LanguageData("ru", "Russian"),
            new LanguageData("de", "German"),
            new LanguageData("es", "Spanish"),
            new LanguageData("fr", "French"),
        ],
        DefaultLanguageCode = "ru",
    };
}
