using StoryTelling.Application.Settings;

namespace StoryTelling.Application.Abstractions;

public interface ISettingsService
{
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}
