using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;

namespace StoryTelling.ViewModels;

public partial class WelcomeViewModel : ViewModelBase
{
    private readonly Action _newProject;
    private readonly Action _openProject;

    public ObservableCollection<string> RecentProjects { get; } =
    [
        "The Ember Crown  ·  embStory.story.json",
        "Station Nine  ·  station9.story.json",
        "The Salt Road  ·  salt-road.story.json",
    ];

    public WelcomeViewModel(Action newProject, Action openProject)
    {
        _newProject = newProject;
        _openProject = openProject;
    }

    [RelayCommand]
    private void NewProject() => _newProject();

    [RelayCommand]
    private void OpenProject() => _openProject();
}
