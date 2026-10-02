using CommunityToolkit.Mvvm.Input;

namespace StoryTelling.ViewModels;

public interface IUndoRedoHost
{
    IAsyncRelayCommand UndoCommand { get; }

    IAsyncRelayCommand RedoCommand { get; }

    string UndoLabel { get; }

    string RedoLabel { get; }
}
