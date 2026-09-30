using StoryTelling.Application.Settings;
using StoryTelling.Infrastructure;

namespace StoryTelling.Tests;

public sealed class JsonSettingsServiceTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "StoryTellingSettingsTests", Guid.NewGuid().ToString("N"));

    public JsonSettingsServiceTests()
    {
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public async Task Load_MissingFile_ReturnsDefaults()
    {
        var service = new JsonSettingsService(Path.Combine(_directory, "settings.json"));

        var settings = await service.LoadAsync();

        Assert.Equal(AppSettings.CurrentSchemaVersion, settings.SchemaVersion);
        Assert.NotEmpty(settings.Languages);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTripsSettings()
    {
        var path = Path.Combine(_directory, "settings.json");
        var service = new JsonSettingsService(path);

        var settings = AppSettings.CreateDefault();
        settings.Provider = "Ollama";
        settings.BaseUrl = "http://localhost:11434/v1";
        settings.ApiKey = "secret-token";
        settings.DefaultLanguageCode = "de";
        settings.RecentProjects.Add(@"C:\stories\one.story.json");

        await service.SaveAsync(settings);
        var loaded = await service.LoadAsync();

        Assert.Equal("Ollama", loaded.Provider);
        Assert.Equal("http://localhost:11434/v1", loaded.BaseUrl);
        Assert.Equal("secret-token", loaded.ApiKey);
        Assert.Equal("de", loaded.DefaultLanguageCode);
        Assert.Equal([@"C:\stories\one.story.json"], loaded.RecentProjects);
    }

    [Fact]
    public async Task Load_IgnoresUnknownFields()
    {
        var path = Path.Combine(_directory, "settings.json");
        await File.WriteAllTextAsync(path, """{ "provider": "OpenAI", "someFutureSetting": 42 }""");

        var service = new JsonSettingsService(path);
        var settings = await service.LoadAsync();

        Assert.Equal("OpenAI", settings.Provider);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
