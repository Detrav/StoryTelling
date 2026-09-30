using StoryTelling.Application.Undo;
using StoryTelling.Infrastructure.Diff;

namespace StoryTelling.Tests;

public sealed class UndoRedoServiceTests
{
    private string _state = "line1\nline2";

    [Fact]
    public async Task UndoAndRedo_RoundTrips()
    {
        var service = Create();

        _state = "line1\nchanged";
        Assert.True(service.Push("Edit line"));

        Assert.True(service.CanUndo);
        Assert.False(service.CanRedo);
        Assert.Equal("Edit line", service.NextUndoName);

        _state = await service.UndoAsync() ?? _state;
        Assert.Equal("line1\nline2", _state);
        Assert.False(service.CanUndo);
        Assert.True(service.CanRedo);
        Assert.Equal("Edit line", service.NextRedoName);

        _state = await service.RedoAsync() ?? _state;
        Assert.Equal("line1\nchanged", _state);
        Assert.True(service.CanUndo);
        Assert.False(service.CanRedo);
    }

    [Fact]
    public void Push_UnchangedState_IsIgnored()
    {
        var service = Create();

        Assert.False(service.Push("Noop"));
        Assert.False(service.CanUndo);
        Assert.Empty(service.History);
    }

    [Fact]
    public async Task Undo_CommitsMissedChange_ThenRevertsIt()
    {
        var service = Create();

        _state = "line1\na";
        service.Push("A");
        _state = "line1\nb";
        service.Push("B");

        _state = await service.UndoAsync() ?? _state;

        _state = "line1\nc";
        var undone = await service.UndoAsync();

        Assert.Equal("line1\na", undone);
        Assert.True(service.CanRedo);
        Assert.Equal("Edit", service.NextRedoName);
    }

    [Fact]
    public async Task Undo_FreshHistory_WithUncommittedChange_RevertsIt()
    {
        _state = string.Empty;
        var service = Create();

        _state = "typed";

        var undone = await service.UndoAsync();

        Assert.Equal(string.Empty, undone);
        Assert.True(service.CanRedo);
        Assert.Null(service.NextUndoName);
    }

    [Fact]
    public async Task Push_DropsRedoTail()
    {
        _state = "a";
        var service = Create();
        _state = "b";
        service.Push("B");
        _state = "c";
        service.Push("C");

        _state = await service.UndoAsync() ?? _state;
        Assert.True(service.CanRedo);
        Assert.Equal(new[] { "B", "C" }, service.History);

        _state = "d";
        service.Push("D");

        Assert.False(service.CanRedo);
        Assert.Equal(new[] { "B", "D" }, service.History);
        Assert.Equal("D", service.NextUndoName);
    }

    [Fact]
    public void Reset_ClearsHistory()
    {
        _state = "a";
        var service = Create();
        _state = "b";
        service.Push("B");

        _state = "c";
        service.Reset("c");

        Assert.False(service.CanUndo);
        Assert.False(service.CanRedo);
        Assert.Empty(service.History);
        Assert.Null(service.NextUndoName);
    }

    [Fact]
    public async Task Push_DuringUndo_IsDropped()
    {
        _state = "one";
        var service = Create();
        _state = "two";
        service.Push("Edit");

        var undo = service.UndoAsync();
        var pushed = service.Push("During");
        var state = await undo;

        Assert.False(pushed);
        Assert.Equal("one", state);
        Assert.Equal(new[] { "Edit" }, service.History);
    }

    [Fact]
    public async Task ConcurrentCalls_AreDropped()
    {
        var service = Create();
        _state = "changed";
        service.Push("Edit");

        var first = service.UndoAsync();
        var second = service.UndoAsync();

        Assert.NotNull(await first);
        Assert.Null(await second);
    }

    private UndoRedoService Create()
    {
        var service = new UndoRedoService(new DiffPlexTextDiff(), () => _state);
        service.Reset(_state);
        return service;
    }
}
