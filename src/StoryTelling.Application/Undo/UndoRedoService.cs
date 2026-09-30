using System.Linq;
using StoryTelling.Application.Abstractions;

namespace StoryTelling.Application.Undo;

public sealed class UndoRedoService : IUndoRedoService
{
    private readonly ITextDiff _diff;
    private readonly Func<string> _capture;
    private readonly List<HistoryEntry> _entries = [];
    private int _index;
    private string _current = string.Empty;
    private bool _busy;

    public UndoRedoService(ITextDiff diff, Func<string> capture)
    {
        _diff = diff;
        _capture = capture;
    }

    public event EventHandler? Changed;

    public bool CanUndo => _index > 0;

    public bool CanRedo => _index < _entries.Count;

    public int CurrentIndex => _index;

    public IReadOnlyList<string> History => _entries.Select(entry => entry.Name).ToList();

    public string? NextUndoName => _index > 0 ? _entries[_index - 1].Name : null;

    public string? NextRedoName => _index < _entries.Count ? _entries[_index].Name : null;

    public string PendingChangeName { get; set; } = "Edit";

    public void Reset(string state)
    {
        if (_busy)
        {
            return;
        }

        _entries.Clear();
        _index = 0;
        _current = state;
        RaiseChanged();
    }

    public bool Push(string name)
    {
        if (_busy)
        {
            return false;
        }

        var state = _capture();
        if (state == _current)
        {
            return false;
        }

        var patch = _diff.CreatePatch(_current, state);
        _current = state;

        if (patch.IsEmpty)
        {
            return false;
        }

        TruncateRedoTail();
        _entries.Add(new HistoryEntry(name, patch));
        _index = _entries.Count;
        RaiseChanged();
        return true;
    }

    public async Task<string?> UndoAsync(CancellationToken cancellationToken = default)
    {
        if (_busy)
        {
            return null;
        }

        _busy = true;
        try
        {
            CommitPending();

            if (_index == 0)
            {
                return null;
            }

            var patch = _entries[_index - 1].Patch;
            var baseline = _current;
            var state = await Task.Run(() => patch.Revert(baseline), cancellationToken);
            _current = state;
            _index--;
            RaiseChanged();
            return state;
        }
        finally
        {
            _busy = false;
        }
    }

    public async Task<string?> RedoAsync(CancellationToken cancellationToken = default)
    {
        if (_busy)
        {
            return null;
        }

        _busy = true;
        try
        {
            CommitPending();

            if (_index >= _entries.Count)
            {
                return null;
            }

            var patch = _entries[_index].Patch;
            var baseline = _current;
            var state = await Task.Run(() => patch.Apply(baseline), cancellationToken);
            _current = state;
            _index++;
            RaiseChanged();
            return state;
        }
        finally
        {
            _busy = false;
        }
    }

    private void CommitPending()
    {
        var live = _capture();
        if (live == _current)
        {
            return;
        }

        var patch = _diff.CreatePatch(_current, live);
        _current = live;

        if (patch.IsEmpty)
        {
            return;
        }

        TruncateRedoTail();
        _entries.Add(new HistoryEntry(PendingChangeName, patch));
        _index = _entries.Count;
        RaiseChanged();
    }

    private void TruncateRedoTail()
    {
        if (_index < _entries.Count)
        {
            _entries.RemoveRange(_index, _entries.Count - _index);
        }
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    private sealed record HistoryEntry(string Name, TextPatch Patch);
}
