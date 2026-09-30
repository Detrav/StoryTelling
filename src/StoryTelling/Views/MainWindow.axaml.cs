using System;
using System.Diagnostics;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using StoryTelling.Infrastructure;
using StoryTelling.ViewModels;

namespace StoryTelling.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _viewModel;

    public MainWindow() => InitializeComponent();

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_viewModel is not null)
        {
            _viewModel.ErrorOccurred -= OnErrorOccurred;
            _viewModel.OpenProjectDialogRequested -= OnOpenProjectDialogRequested;
        }

        _viewModel = DataContext as MainWindowViewModel;

        if (_viewModel is not null)
        {
            _viewModel.ErrorOccurred += OnErrorOccurred;
            _viewModel.OpenProjectDialogRequested += OnOpenProjectDialogRequested;
        }
    }

    private void OnErrorOccurred(string message) => ErrorDialog.Show(this, "Something went wrong", message);

    private async void OnOpenProjectDialogRequested()
    {
        if (_viewModel is null)
        {
            return;
        }

        var path = await PickOpenPathAsync();
        if (path is not null)
        {
            await _viewModel.OpenProjectAsync(path);
        }
    }

    private async void OnOpenProjectClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        var path = await PickOpenPathAsync();
        if (path is not null)
        {
            await _viewModel.OpenProjectAsync(path);
        }
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel?.Workspace is null)
        {
            return;
        }

        var path = _viewModel.Workspace.FilePath ?? await PickSavePathAsync();
        if (path is not null)
        {
            await _viewModel.SaveProjectAsync(path);
        }
    }

    private async void OnSaveAsClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel?.Workspace is null)
        {
            return;
        }

        var path = await PickSavePathAsync();
        if (path is not null)
        {
            await _viewModel.SaveProjectAsync(path);
        }
    }

    private async void OnProjectSetupClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        var setup = _viewModel.CreateSetupViewModel();
        var window = new SetupWindow { DataContext = setup };
        if (await window.ShowDialog<bool>(this))
        {
            _viewModel.ApplySetup(setup);
        }
    }

    private async void OnSettingsClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        var settings = new SettingsWindowViewModel(_viewModel.Settings);
        var window = new SettingsWindow { DataContext = settings };
        if (await window.ShowDialog<bool>(this))
        {
            await _viewModel.SaveSettingsAsync(settings.BuildSettings());
        }
    }

    private void OnOpenLogsClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.LogsDirectory);
            Process.Start(new ProcessStartInfo { FileName = AppPaths.LogsDirectory, UseShellExecute = true });
        }
        catch (Exception exception)
        {
            ErrorDialog.Show(this, "Could not open the logs folder", exception.Message);
        }
    }

    private void OnAboutClick(object? sender, RoutedEventArgs e) => _ = new AboutWindow().ShowDialog(this);

    private void OnExitClick(object? sender, RoutedEventArgs e) => Close();

    private async System.Threading.Tasks.Task<string?> PickOpenPathAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open project",
            AllowMultiple = false,
            FileTypeFilter = [StoryFileType],
        });

        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }

    private async System.Threading.Tasks.Task<string?> PickSavePathAsync()
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save project",
            SuggestedFileName = "book.story.json",
            DefaultExtension = "story.json",
            FileTypeChoices = [StoryFileType],
        });

        return file?.Path.LocalPath;
    }

    private static FilePickerFileType StoryFileType => new("Story project")
    {
        Patterns = ["*.story.json"],
    };
}
