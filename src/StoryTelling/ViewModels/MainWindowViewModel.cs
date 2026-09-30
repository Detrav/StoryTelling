using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace StoryTelling.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private object _content;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProject))]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private WorkspaceViewModel? _workspace;

    public bool HasProject => Workspace is not null;

    public LanguageCatalog Languages { get; } = new();

    public string WindowTitle => Workspace is null
        ? "StoryTelling"
        : $"{(Workspace.IsDirty ? "* " : string.Empty)}{Workspace.ProjectName} — StoryTelling";

    public MainWindowViewModel()
    {
        _content = new WelcomeViewModel(NewProject, OpenProject);
    }

    [RelayCommand]
    private void NewProject() => OpenWorkspace(new WorkspaceViewModel(Languages));

    [RelayCommand]
    private void OpenProject() => OpenWorkspace(new WorkspaceViewModel(Languages));

    [RelayCommand]
    private void Save()
    {
        if (Workspace is not null)
        {
            Workspace.IsDirty = false;
        }
    }

    [RelayCommand]
    private void SaveAs()
    {
    }

    [RelayCommand]
    private void CloseProject() => ShowWelcome();

    public SetupViewModel CreateSetupViewModel() => Workspace?.CreateSetup(Languages) ?? new SetupViewModel(Languages);

    public void ApplySetup(SetupViewModel setup) => Workspace?.ApplySetup(setup);

    private void OpenWorkspace(WorkspaceViewModel workspace)
    {
        DetachWorkspace();

        workspace.PropertyChanged += OnWorkspacePropertyChanged;
        workspace.CloseRequested += ShowWelcome;
        Workspace = workspace;
        Content = workspace;
    }

    private void ShowWelcome()
    {
        DetachWorkspace();
        Workspace = null;
        Content = new WelcomeViewModel(NewProject, OpenProject);
    }

    private void DetachWorkspace()
    {
        if (Workspace is null)
        {
            return;
        }

        Workspace.PropertyChanged -= OnWorkspacePropertyChanged;
        Workspace.CloseRequested -= ShowWelcome;
    }

    private void OnWorkspacePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WorkspaceViewModel.IsDirty) or nameof(WorkspaceViewModel.ProjectName))
        {
            OnPropertyChanged(nameof(WindowTitle));
        }
    }
}
