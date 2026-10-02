using StoryTelling.Application.Abstractions;
using StoryTelling.Application.Llm;
using StoryTelling.Application.Settings;

namespace StoryTelling.Cli;

internal sealed class CliContext : IDisposable
{
    public CliContext(AppSettings settings, HttpClient httpClient, ILlmClient llmClient, ISettingsService settingsService)
    {
        Settings = settings;
        HttpClient = httpClient;
        LlmClient = llmClient;
        SettingsService = settingsService;
    }

    public AppSettings Settings
    {
        get;
    }

    public HttpClient HttpClient
    {
        get;
    }

    public ILlmClient LlmClient
    {
        get;
    }

    public ISettingsService SettingsService
    {
        get;
    }

    public LlmConnection Connection => LlmConnection.From(Settings.BaseUrl, Settings.ApiKey, Settings.TimeoutSeconds);

    public void Dispose() => HttpClient.Dispose();
}
