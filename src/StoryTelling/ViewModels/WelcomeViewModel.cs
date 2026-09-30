using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace StoryTelling.ViewModels;

public partial class WelcomeViewModel : ViewModelBase
{
    private readonly Action _newProject;
    private readonly Action _openProject;
    private readonly Action<string> _openRecent;

    public WelcomeViewModel(
        IEnumerable<string> recentProjects,
        Action newProject,
        Action openProject,
        Action<string> openRecent)
    {
        RecentProjects = new ObservableCollection<string>(recentProjects);
        _newProject = newProject;
        _openProject = openProject;
        _openRecent = openRecent;
    }

    public ObservableCollection<string> RecentProjects { get; }

    [ObservableProperty]
    private string? _selectedRecent;

    [RelayCommand]
    private void NewProject() => _newProject();

    [RelayCommand]
    private void OpenProject() => _openProject();

    [RelayCommand]
    private void OpenRecent(string? path)
    {
        if (!string.IsNullOrEmpty(path))
        {
            _openRecent(path);
        }
    }
}
