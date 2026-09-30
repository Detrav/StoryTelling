using System;
using System.Threading.Tasks;
using Avalonia;

namespace StoryTelling;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        AppServices.Initialize();

        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            if (eventArgs.ExceptionObject is Exception exception)
            {
                AppDiagnostics.Write(exception);
            }
        };

        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
        {
            AppDiagnostics.Write(eventArgs.Exception);
            eventArgs.SetObserved();
        };

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
