using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StoryTelling.ViewModels;

namespace StoryTelling.Tests;

internal sealed class FakeUndoRedoHost : ObservableObject, IUndoRedoHost
{
    public IAsyncRelayCommand UndoCommand { get; } = new AsyncRelayCommand(() => Task.CompletedTask);

    public IAsyncRelayCommand RedoCommand { get; } = new AsyncRelayCommand(() => Task.CompletedTask);

    public string UndoLabel { get; set; } = "Undo";

    public string RedoLabel { get; set; } = "Redo";

    public void Raise(string propertyName) => OnPropertyChanged(propertyName);
}
