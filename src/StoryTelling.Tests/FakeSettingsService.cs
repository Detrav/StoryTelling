using StoryTelling.Application.Abstractions;
using StoryTelling.Application.Settings;

namespace StoryTelling.Tests;

internal sealed class FakeSettingsService : ISettingsService
{
    public AppSettings Settings { get; set; } = AppSettings.CreateDefault();

    public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Settings);

    public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        Settings = settings;
        return Task.CompletedTask;
    }
}
