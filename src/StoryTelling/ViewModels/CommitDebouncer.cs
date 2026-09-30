using System;
using Avalonia.Threading;

namespace StoryTelling.ViewModels;

public sealed class CommitDebouncer
{
    private readonly Action _commit;
    private readonly DispatcherTimer _timer;

    public CommitDebouncer(Action commit, TimeSpan delay)
    {
        _commit = commit;
        _timer = new DispatcherTimer { Interval = delay };
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            _commit();
        };
    }

    public void Trigger()
    {
        _timer.Stop();
        _timer.Start();
    }

    public void CommitNow()
    {
        _timer.Stop();
        _commit();
    }
}
