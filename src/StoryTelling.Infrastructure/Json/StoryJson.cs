using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using StoryTelling.Domain;

namespace StoryTelling.Infrastructure.Json;

public static class StoryJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        TypeInfoResolver = StoryJsonContext.Default,
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Serialize(Project project) => JsonSerializer.Serialize(project, Options);

    public static Project Deserialize(string json) =>
        JsonSerializer.Deserialize<Project>(ProjectMigrations.Migrate(json), Options)
        ?? throw new InvalidDataException("The project file is empty.");
}
