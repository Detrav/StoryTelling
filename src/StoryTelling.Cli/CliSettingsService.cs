using StoryTelling.Application.Abstractions;
using StoryTelling.Application.Settings;

namespace StoryTelling.Cli;

internal sealed class CliSettingsService : ISettingsService
{
    private readonly AppSettings _settings;

    public CliSettingsService(AppSettings settings) => _settings = settings;

    public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(_settings);

    public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
