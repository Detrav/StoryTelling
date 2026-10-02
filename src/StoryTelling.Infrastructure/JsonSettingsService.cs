using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using StoryTelling.Application.Abstractions;
using StoryTelling.Application.Settings;
using StoryTelling.Infrastructure.Json;

namespace StoryTelling.Infrastructure;

public sealed class JsonSettingsService : ISettingsService
{
    private static readonly UTF8Encoding _utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    private static readonly JsonSerializerOptions _options = new()
    {
        TypeInfoResolver = SettingsJsonContext.Default,
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly string _path;

    public JsonSettingsService(string path) => _path = path;

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path))
        {
            return ApplyEnvironmentOverrides(AppSettings.CreateDefault());
        }

        var json = await File.ReadAllTextAsync(_path, cancellationToken).ConfigureAwait(false);
        var settings = JsonSerializer.Deserialize<AppSettings>(json, _options);
        return ApplyEnvironmentOverrides(settings ?? AppSettings.CreateDefault());
    }

    private static AppSettings ApplyEnvironmentOverrides(AppSettings settings)
    {
        settings.BaseUrl = Override("STORYTELLING_BASE_URL", settings.BaseUrl);
        settings.Model = Override("STORYTELLING_MODEL", settings.Model);
        settings.ApiKey = Override("STORYTELLING_API_KEY", settings.ApiKey);
        return settings;
    }

    private static string Override(string variable, string fallback) =>
        Environment.GetEnvironmentVariable(variable)?.Trim() is { Length: > 0 } value ? value : fallback;

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        var persisted = HasEnvironmentApiKey()
            ? CopyWithoutEnvironmentKey(settings)
            : settings;

        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(persisted, _options);
        var tempPath = _path + ".tmp";
        await File.WriteAllTextAsync(tempPath, json, _utf8WithoutBom, cancellationToken).ConfigureAwait(false);
        File.Move(tempPath, _path, overwrite: true);
    }

    private static bool HasEnvironmentApiKey() =>
        Environment.GetEnvironmentVariable("STORYTELLING_API_KEY")?.Trim() is { Length: > 0 };

    private static AppSettings CopyWithoutEnvironmentKey(AppSettings settings) => new()
    {
        SchemaVersion = settings.SchemaVersion,
        Provider = settings.Provider,
        BaseUrl = settings.BaseUrl,
        Model = settings.Model,
        TimeoutSeconds = settings.TimeoutSeconds,
        MaxTokens = settings.MaxTokens,
        MaxToolCalls = settings.MaxToolCalls,
        ContextTokenBudget = settings.ContextTokenBudget,
        RecentLoglineCount = settings.RecentLoglineCount,
        ContextRequiredSectionMaxChars = settings.ContextRequiredSectionMaxChars,
        ToolResultMaxChars = settings.ToolResultMaxChars,
        Temperature = settings.Temperature,
        RoleTemperatures = new Dictionary<string, double>(settings.RoleTemperatures),
        DefaultLanguageCode = settings.DefaultLanguageCode,
        Languages = [.. settings.Languages],
        RecentProjects = [.. settings.RecentProjects],
    };
}
