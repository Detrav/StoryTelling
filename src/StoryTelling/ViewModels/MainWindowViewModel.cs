using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using StoryTelling.Application.Abstractions;
using StoryTelling.Application.Chapters;
using StoryTelling.Application.Generation;
using StoryTelling.Application.Knowledge;
using StoryTelling.Application.Review;
using StoryTelling.Application.Settings;
using StoryTelling.Application.Translation;
using StoryTelling.Application.Undo;
using StoryTelling.Domain;
using StoryTelling.Infrastructure.Json;

namespace StoryTelling.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, IUndoRedoHost
{
    private readonly IProjectRepository _repository;
    private readonly ISettingsService _settingsService;
    private readonly IClock _clock;
    private readonly ITextDiff _textDiff;
    private readonly ILlmClient _llmClient;
    private readonly IGenerationAssistant _assistant;
    private readonly IKnowledgeImporter _importer;
    private readonly IProjectReviewAssistant _review;
    private readonly IChapterRunner _chapterRunner;
    private readonly ITranslationService _translationService;
    private readonly ILogger<MainWindowViewModel> _logger;
    private AppSettings _settings = AppSettings.CreateDefault();
    private IUndoRedoService? _undoRedo;

    public MainWindowViewModel(
        IProjectRepository repository,
        ISettingsService settingsService,
        IClock clock,
        ITextDiff textDiff,
        ILlmClient llmClient,
        IGenerationAssistant assistant,
        IKnowledgeImporter importer,
        IProjectReviewAssistant review,
        IChapterRunner chapterRunner,
        ITranslationService translationService,
        ILogger<MainWindowViewModel> logger)
    {
        _repository = repository;
        _settingsService = settingsService;
        _clock = clock;
        _textDiff = textDiff;
        _llmClient = llmClient;
        _assistant = assistant;
        _importer = importer;
        _review = review;
        _chapterRunner = chapterRunner;
        _translationService = translationService;
        _logger = logger;
        _content = new WelcomeViewModel(_settings.RecentProjects, NewProject, RequestOpenProject, OpenRecent);
    }

    public event Action<string>? ErrorOccurred;

    public event Action? OpenProjectDialogRequested;

    public event Action? SaveRequested;

    public event Action? SaveAsRequested;

    [ObservableProperty]
    private object _content;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProject))]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private WorkspaceViewModel? _workspace;

    public AppSettings Settings => _settings;

    public ITextDiff TextDiff => _textDiff;

    public ILlmClient LlmClient => _llmClient;

    public bool HasProject => Workspace is not null;

    public bool CanUndo => _undoRedo is not null;

    public bool CanRedo => _undoRedo?.CanRedo == true;

    public string UndoLabel => _undoRedo?.NextUndoName is { Length: > 0 } name ? $"Undo: {name}" : "Undo";

    public string RedoLabel => _undoRedo?.NextRedoName is { Length: > 0 } name ? $"Redo: {name}" : "Redo";

    public string WindowTitle => Workspace is null
        ? "StoryTelling"
        : $"{(Workspace.IsDirty ? "* " : string.Empty)}{Workspace.ProjectName} — StoryTelling";

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
        OpenWorkspace(new WorkspaceViewModel(CreateNewProject(), _clock, _chapterRunner, _assistant, _translationService) { IsDirty = true }, resetUndo: true);
        _logger.LogInformation("New project created");
    }

    [RelayCommand]
    private void CloseProject() => ShowWelcome();

    [RelayCommand]
    private void OpenProject() => RequestOpenProject();

    [RelayCommand]
    private void Save() => SaveRequested?.Invoke();

    [RelayCommand]
    private void SaveAs() => SaveAsRequested?.Invoke();

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private async Task UndoAsync()
    {
        if (_undoRedo is null)
        {
            return;
        }

        var state = await _undoRedo.UndoAsync();
        if (state is not null)
        {
            ApplyState(state);
        }
    }

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private async Task RedoAsync()
    {
        if (_undoRedo is null)
        {
            return;
        }

        var state = await _undoRedo.RedoAsync();
        if (state is not null)
        {
            ApplyState(state);
        }
    }

    public void RequestOpenProject() => OpenProjectDialogRequested?.Invoke();

    public async Task OpenProjectAsync(string path)
    {
        try
        {
            var project = await _repository.LoadAsync(path);
            OpenWorkspace(new WorkspaceViewModel(project, _clock, _chapterRunner, _assistant, _translationService) { FilePath = path }, resetUndo: true);
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
        Workspace?.CreateSetup(_settings.Languages, _textDiff, _assistant, _importer, _review)
        ?? new SetupViewModel(_textDiff, _settings.Languages, [], _assistant, _importer, _review);

    public void ApplySetup(SetupViewModel setup)
    {
        if (Workspace is null)
        {
            return;
        }

        Workspace.ApplySetup(setup);
        _undoRedo?.Push("Project setup");
    }

    private void OpenWorkspace(WorkspaceViewModel workspace, bool resetUndo)
    {
        Detach();
        workspace.PropertyChanged += OnWorkspacePropertyChanged;
        workspace.CloseRequested += CloseProject;
        workspace.Mutated += OnWorkspaceMutated;
        Workspace = workspace;
        Content = workspace;

        if (resetUndo)
        {
            ResetUndo();
        }
        else
        {
            NotifyUndoRedo();
        }
    }

    private void ShowWelcome()
    {
        Detach();
        DetachUndo();
        Workspace = null;
        Content = new WelcomeViewModel(_settings.RecentProjects, NewProject, RequestOpenProject, OpenRecent);
    }

    private void OpenRecent(string path) => _ = OpenProjectAsync(path);

    private void OnWorkspaceMutated(string name)
    {
        if (Workspace is not null)
        {
            Workspace.IsDirty = true;
        }

        _undoRedo?.Push(name);
    }

    private void ApplyState(string state)
    {
        var project = StoryJson.Deserialize(state);
        var selectedNumber = Workspace?.SelectedChapter?.Number ?? 1;
        var tabIndex = Workspace?.SelectedTabIndex ?? 0;
        var path = Workspace?.FilePath;
        var sidebar = Workspace?.IsSidebarVisible ?? true;

        var workspace = new WorkspaceViewModel(project, _clock, _chapterRunner, _assistant, _translationService)
        {
            FilePath = path,
            IsSidebarVisible = sidebar,
            IsDirty = true,
            SelectedTabIndex = tabIndex,
        };

        var chapter = workspace.Chapters.FirstOrDefault(candidate => candidate.Number == selectedNumber);
        workspace.SelectedChapter = chapter ?? workspace.Chapters[0];

        OpenWorkspace(workspace, resetUndo: false);
    }

    private void ResetUndo()
    {
        DetachUndo();

        if (Workspace is null)
        {
            return;
        }

        _undoRedo = new UndoRedoService(_textDiff, () => StoryJson.Serialize(Workspace.ToProject()));
        _undoRedo.Changed += OnUndoRedoChanged;
        _undoRedo.Reset(StoryJson.Serialize(Workspace.ToProject()));
        NotifyUndoRedo();
    }

    private void DetachUndo()
    {
        if (_undoRedo is not null)
        {
            _undoRedo.Changed -= OnUndoRedoChanged;
            _undoRedo = null;
        }
    }

    private void OnUndoRedoChanged(object? sender, EventArgs e) => NotifyUndoRedo();

    private void NotifyUndoRedo()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(UndoLabel));
        OnPropertyChanged(nameof(RedoLabel));
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    private void Detach()
    {
        if (Workspace is null)
        {
            return;
        }

        Workspace.PropertyChanged -= OnWorkspacePropertyChanged;
        Workspace.CloseRequested -= CloseProject;
        Workspace.Mutated -= OnWorkspaceMutated;
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
        };
    }
}
