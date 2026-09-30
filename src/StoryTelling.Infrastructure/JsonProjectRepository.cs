using System.Text;
using StoryTelling.Application.Abstractions;
using StoryTelling.Domain;
using StoryTelling.Infrastructure.Json;

namespace StoryTelling.Infrastructure;

public sealed class JsonProjectRepository : IProjectRepository
{
    private static readonly UTF8Encoding _utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    public async Task<Project> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        var json = await File.ReadAllTextAsync(path, cancellationToken);
        var project = StoryJson.Deserialize(json);

        if (project.SchemaVersion > ProjectSchema.Version)
        {
            throw new NotSupportedException(
                $"Project schema version {project.SchemaVersion} is newer than the supported version {ProjectSchema.Version}.");
        }

        return project;
    }

    public async Task SaveAsync(Project project, string path, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        project.SchemaVersion = ProjectSchema.Version;
        var json = StoryJson.Serialize(project);

        var tempPath = fullPath + ".tmp";
        await File.WriteAllTextAsync(tempPath, json, _utf8WithoutBom, cancellationToken);
        File.Move(tempPath, fullPath, overwrite: true);
    }
}
