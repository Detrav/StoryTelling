using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using StoryTelling.Application.Abstractions;
using StoryTelling.Application.Settings;
using StoryTelling.Domain;

namespace StoryTelling.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IProjectRepository _repository;
    private readonly ISettingsService _settingsService;
    private readonly IClock _clock;
    private readonly ILogger<MainWindowViewModel> _logger;
    private AppSettings _settings = AppSettings.CreateDefault();

    public event Action<string>? ErrorOccurred;

    public event Action? OpenProjectDialogRequested;

    [ObservableProperty]
    private object _content;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProject))]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private WorkspaceViewModel? _workspace;

    public AppSettings Settings => _settings;

    public bool HasProject => Workspace is not null;

    public string WindowTitle => Workspace is null
        ? "StoryTelling"
        : $"{(Workspace.IsDirty ? "* " : string.Empty)}{Workspace.ProjectName} — StoryTelling";

    public MainWindowViewModel(
        IProjectRepository repository,
        ISettingsService settingsService,
        IClock clock,
        ILogger<MainWindowViewModel> logger)
    {
        _repository = repository;
        _settingsService = settingsService;
        _clock = clock;
        _logger = logger;
        _content = new WelcomeViewModel(_settings.RecentProjects, NewProject, RequestOpenProject, OpenRecent);
    }

    public async Task InitializeAsync()
    {
        try
        {
            _settings = await _settingsService.LoadAsync();
            _logger.LogInformation("Settings loaded ({RecentCount} recent projects)", _settings.RecentProjects.Count);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to load settings; using defaults");
            _settings = AppSettings.CreateDefault();
        }

        ShowWelcome();
    }

    [RelayCommand]
    private void NewProject()
    {
        OpenWorkspace(new WorkspaceViewModel(CreateNewProject()) { IsDirty = true });
        _logger.LogInformation("New project created");
    }

    [RelayCommand]
    private void CloseProject() => ShowWelcome();

    public void RequestOpenProject() => OpenProjectDialogRequested?.Invoke();

    public async Task OpenProjectAsync(string path)
    {
        try
        {
            var project = await _repository.LoadAsync(path);
            OpenWorkspace(new WorkspaceViewModel(project) { FilePath = path });
            AddRecent(path);
            _logger.LogInformation("Opened project {Path}", path);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to open project {Path}", path);
            ErrorOccurred?.Invoke($"Could not open the project.\n\n{exception.Message}");
        }
    }

    public async Task SaveProjectAsync(string path)
    {
        if (Workspace is null)
        {
            return;
        }

        try
        {
            var project = Workspace.ToProject();
            project.UpdatedUtc = _clock.UtcNow;
            await _repository.SaveAsync(project, path);
            Workspace.FilePath = path;
            Workspace.IsDirty = false;
            AddRecent(path);
            _logger.LogInformation("Saved project {Path}", path);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to save project {Path}", path);
            ErrorOccurred?.Invoke($"Could not save the project.\n\n{exception.Message}");
        }
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        try
        {
            await _settingsService.SaveAsync(settings);
            _settings = settings;
            _logger.LogInformation("Settings saved");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to save settings");
            ErrorOccurred?.Invoke($"Could not save settings.\n\n{exception.Message}");
        }
    }

    public SetupViewModel CreateSetupViewModel() =>
        Workspace?.CreateSetup(_settings.Languages) ?? new SetupViewModel(_settings.Languages, []);

    public void ApplySetup(SetupViewModel setup) => Workspace?.ApplySetup(setup);

    private void OpenWorkspace(WorkspaceViewModel workspace)
    {
        Detach();
        workspace.PropertyChanged += OnWorkspacePropertyChanged;
        workspace.CloseRequested += CloseProject;
        Workspace = workspace;
        Content = workspace;
    }

    private void ShowWelcome()
    {
        Detach();
        Workspace = null;
        Content = new WelcomeViewModel(_settings.RecentProjects, NewProject, RequestOpenProject, OpenRecent);
    }

    private void OpenRecent(string path) => _ = OpenProjectAsync(path);

    private void Detach()
    {
        if (Workspace is null)
        {
            return;
        }

        Workspace.PropertyChanged -= OnWorkspacePropertyChanged;
        Workspace.CloseRequested -= CloseProject;
    }

    private void OnWorkspacePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WorkspaceViewModel.IsDirty) or nameof(WorkspaceViewModel.ProjectName))
        {
            OnPropertyChanged(nameof(WindowTitle));
        }
    }

    private void AddRecent(string path)
    {
        _settings.RecentProjects.Remove(path);
        _settings.RecentProjects.Insert(0, path);
        while (_settings.RecentProjects.Count > 10)
        {
            _settings.RecentProjects.RemoveAt(_settings.RecentProjects.Count - 1);
        }

        _ = PersistRecentAsync();
    }

    private async Task PersistRecentAsync()
    {
        try
        {
            await _settingsService.SaveAsync(_settings);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to persist recent projects");
        }
    }

    private Project CreateNewProject()
    {
        var defaultCode = _settings.DefaultLanguageCode;
        var targets = _settings.Languages.Any(language => language.Code == defaultCode)
            ? new List<string> { defaultCode }
            : new List<string>();

        return new Project
        {
            Name = "Untitled",
            CreatedUtc = _clock.UtcNow,
            UpdatedUtc = _clock.UtcNow,
            Settings = new StorySettings { TargetLanguages = targets },
            Plot = new PlotDescription { ChapterCount = 3 },
        };
    }
}
