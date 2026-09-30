using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using StoryTelling.Application.Abstractions;
using StoryTelling.Application.Undo;

namespace StoryTelling.ViewModels;

public abstract class UndoableDialogViewModel : ViewModelBase, IUndoRedoHost
{
    private readonly ITextDiff _diff;
    private UndoRedoService? _undoRedo;

    protected UndoableDialogViewModel(ITextDiff diff)
    {
        _diff = diff;
        UndoCommand = new AsyncRelayCommand(UndoAsync, () => _undoRedo is not null);
        RedoCommand = new AsyncRelayCommand(RedoAsync, () => _undoRedo?.CanRedo == true);
    }

    public IAsyncRelayCommand UndoCommand { get; }

    public IAsyncRelayCommand RedoCommand { get; }

    public string UndoLabel => _undoRedo?.NextUndoName is { Length: > 0 } name ? $"Undo: {name}" : "Undo";

    public string RedoLabel => _undoRedo?.NextRedoName is { Length: > 0 } name ? $"Redo: {name}" : "Redo";

    protected abstract string CaptureState();

    protected abstract void ApplyState(string state);

    protected void InitializeUndo()
    {
        _undoRedo = new UndoRedoService(_diff, CaptureState);
        _undoRedo.Changed += OnUndoChanged;
        _undoRedo.Reset(CaptureState());
        RaiseUndoState();
    }

    public void Commit() => _undoRedo?.Push("Edit");

    protected void PushUndo(string name) => _undoRedo?.Push(name);

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

    private void OnUndoChanged(object? sender, EventArgs e) => RaiseUndoState();

    private void RaiseUndoState()
    {
        OnPropertyChanged(nameof(UndoLabel));
        OnPropertyChanged(nameof(RedoLabel));
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }
}
