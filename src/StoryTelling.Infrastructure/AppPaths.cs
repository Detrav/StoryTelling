namespace StoryTelling.Infrastructure;

public static class AppPaths
{
    public static string ConfigDirectory { get; } = ResolveConfigDirectory();

    public static string SettingsFile => Path.Combine(ConfigDirectory, "settings.json");

    public static string LogsDirectory => Path.Combine(ConfigDirectory, "logs");

    public const int MaxLogFiles = 20;

    public static string CreateLogFilePath() =>
        Path.Combine(LogsDirectory, $"app-{DateTime.Now:yyyyMMdd-HHmmss}.log");

    public static void PruneOldLogs()
    {
        if (!Directory.Exists(LogsDirectory))
        {
            return;
        }

        var files = new DirectoryInfo(LogsDirectory)
            .GetFiles("app-*.log")
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Skip(MaxLogFiles);

        foreach (var file in files)
        {
            file.Delete();
        }
    }

    private static string ResolveConfigDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "StoryTelling");
        }

        var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (!string.IsNullOrWhiteSpace(xdg))
        {
            return Path.Combine(xdg, "StoryTelling");
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".config", "StoryTelling");
    }
}
