namespace StoryTelling.Application.Undo;

public interface IUndoRedoService
{
    bool CanUndo { get; }

    bool CanRedo { get; }

    int CurrentIndex { get; }

    IReadOnlyList<string> History { get; }

    string? NextUndoName { get; }

    string? NextRedoName { get; }

    string PendingChangeName { get; set; }

    event EventHandler? Changed;

    void Reset(string state);

    bool Push(string name);

    Task<string?> UndoAsync(CancellationToken cancellationToken = default);

    Task<string?> RedoAsync(CancellationToken cancellationToken = default);
}
