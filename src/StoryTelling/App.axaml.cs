using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StoryTelling.ViewModels;
using StoryTelling.Views;

namespace StoryTelling;

public partial class App : Avalonia.Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            try
            {
                var logger = AppServices.Provider.GetRequiredService<ILogger<App>>();
                var viewModel = AppServices.Provider.GetRequiredService<MainWindowViewModel>();

                desktop.MainWindow = new MainWindow { DataContext = viewModel };
                desktop.Exit += (_, _) => AppServices.Provider.Dispose();

                _ = viewModel.InitializeAsync();

                logger.LogInformation("Application started");
            }
            catch (Exception exception)
            {
                AppDiagnostics.Write(exception);
                throw;
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
