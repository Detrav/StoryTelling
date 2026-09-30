using System;
using System.IO;
using StoryTelling.Infrastructure;

namespace StoryTelling;

internal static class AppDiagnostics
{
    public static string CrashFile => Path.Combine(AppPaths.ConfigDirectory, "crash.log");

    public static void Write(Exception exception)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.ConfigDirectory);
            File.AppendAllText(CrashFile, $"{DateTimeOffset.Now:O}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
            Console.Error.WriteLine(exception);
        }
        catch
        {
        }
    }
}
