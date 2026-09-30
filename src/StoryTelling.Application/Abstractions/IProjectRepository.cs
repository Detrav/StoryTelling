using StoryTelling.Domain;

namespace StoryTelling.Application.Abstractions;

public interface IProjectRepository
{
    Task<Project> LoadAsync(string path, CancellationToken cancellationToken = default);

    Task SaveAsync(Project project, string path, CancellationToken cancellationToken = default);
}
