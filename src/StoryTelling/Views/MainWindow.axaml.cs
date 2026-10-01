using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using StoryTelling.Application.Export;
using StoryTelling.Infrastructure;
using StoryTelling.ViewModels;

namespace StoryTelling.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        UndoRedoKeyboard.Attach(this);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_viewModel is not null)
        {
            _viewModel.ErrorOccurred -= OnErrorOccurred;
            _viewModel.WarningOccurred -= OnWarningOccurred;
            _viewModel.OpenProjectDialogRequested -= OnOpenProjectDialogRequested;
            _viewModel.SaveRequested -= OnSaveRequested;
            _viewModel.SaveAsRequested -= OnSaveAsRequested;
            _viewModel.SaveBeforeCloseRequested -= SaveBeforeCloseAsync;
        }

        _viewModel = DataContext as MainWindowViewModel;

        if (_viewModel is not null)
        {
            _viewModel.ErrorOccurred += OnErrorOccurred;
            _viewModel.WarningOccurred += OnWarningOccurred;
            _viewModel.OpenProjectDialogRequested += OnOpenProjectDialogRequested;
            _viewModel.SaveRequested += OnSaveRequested;
            _viewModel.SaveAsRequested += OnSaveAsRequested;
            _viewModel.SaveBeforeCloseRequested += SaveBeforeCloseAsync;
        }
    }

    private void OnErrorOccurred(string message) => ErrorDialog.Show(this, "Something went wrong", message);

    private void OnWarningOccurred(string title, string message) => ErrorDialog.Show(this, title, message);

    private async void OnOpenProjectDialogRequested() => await GuardedAsync(OpenProjectAsync);

    private async void OnOpenProjectClick(object? sender, RoutedEventArgs e) => await GuardedAsync(OpenProjectAsync);

    private async void OnSaveRequested() => await GuardedAsync(SaveAsync);

    private async void OnSaveAsRequested() => await GuardedAsync(SaveAsAsync);

    private async void OnSaveClick(object? sender, RoutedEventArgs e) => await GuardedAsync(SaveAsync);

    private async void OnSaveAsClick(object? sender, RoutedEventArgs e) => await GuardedAsync(SaveAsAsync);

    private async void OnExportFb2Click(object? sender, RoutedEventArgs e) => await GuardedAsync(ExportFb2Async);

    private async void OnCompleteBookClick(object? sender, RoutedEventArgs e) => await GuardedAsync(CompleteBookAsync);

    private async void OnTranslateMetadataClick(object? sender, RoutedEventArgs e) => await GuardedAsync(TranslateMetadataAsync);

    private async Task CompleteBookAsync()
    {
        if (_viewModel?.Workspace is not { } workspace)
        {
            return;
        }

        var viewModel = new BookCompletionViewModel(workspace.BuildCompletionPlan, workspace.CompleteBookAsync);
        var window = new BookCompletionWindow { DataContext = viewModel };
        await window.ShowDialog(this);
    }

    private async Task TranslateMetadataAsync()
    {
        if (_viewModel?.Workspace is not { } workspace || workspace.IsBusy)
        {
            return;
        }

        var viewModel = new MetadataTranslationViewModel(workspace.MetadataLanguages, workspace.NeedsMetadataTranslation, workspace.TranslateMetadataAsync);
        var window = new MetadataTranslationWindow { DataContext = viewModel };
        await window.ShowDialog(this);
    }

    private async void OnProjectSetupClick(object? sender, RoutedEventArgs e) => await GuardedAsync(OpenSetupAsync);

    private async void OnSettingsClick(object? sender, RoutedEventArgs e) => await GuardedAsync(OpenSettingsAsync);

    private async Task GuardedAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            ErrorDialog.Show(this, "Something went wrong", exception.Message);
        }
    }

    private async Task OpenProjectAsync()
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

    private async Task SaveAsync()
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

    private async Task SaveAsAsync()
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

    private async Task ExportFb2Async()
    {
        if (_viewModel?.Workspace is not { } workspace)
        {
            return;
        }

        var project = workspace.ToProject();
        var viewModel = new ExportViewModel(project, _viewModel.Settings.Languages);
        var window = new ExportWindow { DataContext = viewModel };
        if (!await window.ShowDialog<bool>(this) || viewModel.Selected is not { } language)
        {
            return;
        }

        if (!language.IsOriginal && (!language.MetadataComplete || language.Translated < language.Total))
        {
            var reasons = new List<string>();
            if (language.Translated < language.Total)
            {
                reasons.Add($"{language.Total - language.Translated} of {language.Total} chapters are not translated to {language.DisplayName}; the original English text will be used for them");
            }

            if (!language.MetadataComplete)
            {
                reasons.Add(language.MetadataOnlyStale
                    ? "the cached book metadata is out of date and will be exported as it is; run Translate book metadata to refresh it"
                    : $"the book metadata is incomplete ({language.MetadataCoverage}); the English text will be used for the missing parts");
            }

            if (!await ConfirmDialog.ShowAsync(this, "Incomplete export", string.Join("\n\n", reasons) + "\n\nExport anyway?"))
            {
                return;
            }
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export FB2",
            SuggestedFileName = $"{SafeFileName(project.Name)}.fb2",
            DefaultExtension = "fb2",
            FileTypeChoices = [Fb2FileType],
        });

        if (file is null)
        {
            return;
        }

        var fb2 = Fb2Exporter.Build(project, language.Code);
        await File.WriteAllTextAsync(file.Path.LocalPath, fb2, new UTF8Encoding(false));
    }

    private async Task OpenSetupAsync()
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

    private async Task OpenSettingsAsync()
    {
        if (_viewModel is null)
        {
            return;
        }

        var settings = new SettingsWindowViewModel(_viewModel.TextDiff, _viewModel.Settings, _viewModel.LlmClient);
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

    private void OnAboutClick(object? sender, RoutedEventArgs e) => _ = new AboutWindow { DataContext = new AboutViewModel() }.ShowDialog(this);

    private void OnExitClick(object? sender, RoutedEventArgs e) => _ = GuardedAsync(ExitAsync);

    private async Task ExitAsync()
    {
        if (_viewModel is not null && await _viewModel.ConfirmCloseAsync())
        {
            Close();
        }
    }

    private async Task<bool> SaveBeforeCloseAsync(string projectName)
    {
        switch (await UnsavedChangesDialog.ShowAsync(this, projectName))
        {
            case UnsavedChangesChoice.Discard:
                return true;
            case UnsavedChangesChoice.Save:
                await SaveAsync();
                return _viewModel?.Workspace?.IsDirty != true;
            default:
                return false;
        }
    }

    private async Task<string?> PickOpenPathAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open project",
            AllowMultiple = false,
            FileTypeFilter = [StoryFileType],
        });

        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }

    private async Task<string?> PickSavePathAsync()
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save project",
            SuggestedFileName = "book.story.json",
            DefaultExtension = "json",
            FileTypeChoices = [StoryFileType],
        });

        return file?.Path.LocalPath;
    }

    private static FilePickerFileType StoryFileType => new("Story project")
    {
        Patterns = ["*.story.json"],
    };

    private static FilePickerFileType Fb2FileType => new("FictionBook")
    {
        Patterns = ["*.fb2"],
    };

    private static string SafeFileName(string name)
    {
        var trimmed = string.IsNullOrWhiteSpace(name) ? "book" : name.Trim();
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            trimmed = trimmed.Replace(invalid, '_');
        }

        return trimmed;
    }
}
