using Microsoft.Extensions.Logging;
using StoryTelling.Application.Abstractions;
using StoryTelling.Application.Llm;
using StoryTelling.Application.Settings;

namespace StoryTelling.Cli;

internal sealed class CliContext : IDisposable
{
    public CliContext(
        AppSettings settings,
        HttpClient httpClient,
        ILlmClient llmClient,
        ISettingsService settingsService,
        ILoggerFactory loggerFactory,
        string logPath)
    {
        Settings = settings;
        HttpClient = httpClient;
        LlmClient = llmClient;
        SettingsService = settingsService;
        LoggerFactory = loggerFactory;
        LogPath = logPath;
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

    public ILoggerFactory LoggerFactory
    {
        get;
    }

    public string LogPath
    {
        get;
    }

    public LlmConnection Connection => LlmConnection.From(Settings.BaseUrl, Settings.ApiKey, Settings.TimeoutSeconds);

    public void Dispose()
    {
        HttpClient.Dispose();
        LoggerFactory.Dispose();
    }
}
