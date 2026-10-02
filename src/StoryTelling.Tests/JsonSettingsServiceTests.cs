using System;
using System.Linq;
using System.Threading.Tasks;
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

    [Fact]
    public async Task Load_EnvironmentOverrides_WinOverStoredValues()
    {
        var path = Path.Combine(_directory, "settings.json");
        var service = new JsonSettingsService(path);
        var stored = AppSettings.CreateDefault();
        stored.BaseUrl = "https://stored.example/v1";
        stored.Model = "stored-model";
        stored.ApiKey = "stored-key";
        await service.SaveAsync(stored);

        await WithEnvironmentAsync(
            [("STORYTELLING_BASE_URL", "https://env.example/v1"), ("STORYTELLING_MODEL", "env-model"), ("STORYTELLING_API_KEY", "env-key")],
            async () =>
            {
                var settings = await service.LoadAsync();

                Assert.Equal("https://env.example/v1", settings.BaseUrl);
                Assert.Equal("env-model", settings.Model);
                Assert.Equal("env-key", settings.ApiKey);
            });
    }

    [Fact]
    public async Task Load_EmptyEnvironmentVariables_AreIgnored()
    {
        var path = Path.Combine(_directory, "settings.json");
        var service = new JsonSettingsService(path);
        var stored = AppSettings.CreateDefault();
        stored.BaseUrl = "https://stored.example/v1";
        await service.SaveAsync(stored);

        await WithEnvironmentAsync(
            [("STORYTELLING_BASE_URL", "   "), ("STORYTELLING_MODEL", string.Empty)],
            async () =>
            {
                var settings = await service.LoadAsync();

                Assert.Equal("https://stored.example/v1", settings.BaseUrl);
                Assert.Equal(AppSettings.CreateDefault().Model, settings.Model);
            });
    }

    [Fact]
    public async Task Save_WithEnvironmentApiKey_DoesNotPersistTheSecret()
    {
        var path = Path.Combine(_directory, "settings.json");
        var service = new JsonSettingsService(path);

        await WithEnvironmentAsync([("STORYTELLING_API_KEY", "env-key")], async () =>
        {
            var settings = AppSettings.CreateDefault();
            settings.Provider = "Ollama";
            settings.Temperature = 0.42;
            settings.RecentProjects.Add(@"C:\stories\one.story.json");
            settings.Languages.Add(new LanguageData("ru", "Russian"));

            await service.SaveAsync(settings);
        });

        var json = await File.ReadAllTextAsync(path);
        Assert.DoesNotContain("env-key", json);

        var reloaded = await service.LoadAsync();
        Assert.Equal("Ollama", reloaded.Provider);
        Assert.Equal(0.42, reloaded.Temperature);
        Assert.Equal([@"C:\stories\one.story.json"], reloaded.RecentProjects);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTripsRoleTemperatures()
    {
        var path = Path.Combine(_directory, "settings.json");
        var service = new JsonSettingsService(path);
        var settings = AppSettings.CreateDefault();
        settings.RoleTemperatures["review"] = 0.5;

        await service.SaveAsync(settings);
        var loaded = await service.LoadAsync();

        Assert.Equal(0.5, loaded.RoleTemperatures["review"]);
    }

    [Fact]
    public async Task Save_WithEnvironmentApiKey_PreservesBudgetsAndRoleTemperatures()
    {
        var path = Path.Combine(_directory, "settings.json");
        var service = new JsonSettingsService(path);

        await WithEnvironmentAsync([("STORYTELLING_API_KEY", "env-key")], async () =>
        {
            var settings = AppSettings.CreateDefault();
            settings.ContextTokenBudget = 7777;
            settings.RoleTemperatures["writer"] = 0.55;
            await service.SaveAsync(settings);
        });

        var reloaded = await service.LoadAsync();
        Assert.Equal(7777, reloaded.ContextTokenBudget);
        Assert.Equal(0.55, reloaded.RoleTemperatures["writer"]);
    }

    [Fact]
    public async Task Save_WithoutEnvironmentApiKey_PersistsTheConfiguredKey()
    {
        var path = Path.Combine(_directory, "settings.json");
        var service = new JsonSettingsService(path);
        var settings = AppSettings.CreateDefault();
        settings.ApiKey = "user-key";

        await service.SaveAsync(settings);

        Assert.Contains("user-key", await File.ReadAllTextAsync(path));
    }

    private static async Task WithEnvironmentAsync((string Name, string Value)[] variables, Func<Task> action)
    {
        var previous = variables
            .Select(variable => (variable.Name, Value: Environment.GetEnvironmentVariable(variable.Name)))
            .ToArray();

        foreach (var variable in variables)
        {
            Environment.SetEnvironmentVariable(variable.Name, variable.Value);
        }

        try
        {
            await action();
        }
        finally
        {
            foreach (var entry in previous)
            {
                Environment.SetEnvironmentVariable(entry.Name, entry.Value);
            }
        }
    }

    public void Dispose()
    {
        foreach (var name in new[] { "STORYTELLING_BASE_URL", "STORYTELLING_MODEL", "STORYTELLING_API_KEY" })
        {
            Environment.SetEnvironmentVariable(name, null);
        }

        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
