using System;
using Avalonia.Threading;

namespace StoryTelling.ViewModels;

public sealed class CommitDebouncer
{
    private readonly Action _commit;
    private readonly TimeSpan _delay;
    private DispatcherTimer? _timer;

    public CommitDebouncer(Action commit, TimeSpan delay)
    {
        _commit = commit;
        _delay = delay;
    }

    public void Trigger()
    {
        var timer = _timer ??= CreateTimer();
        timer.Stop();
        timer.Start();
    }

    public void CommitNow()
    {
        _timer?.Stop();
        _commit();
    }

    private DispatcherTimer CreateTimer()
    {
        var timer = new DispatcherTimer { Interval = _delay };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            _commit();
        };

        return timer;
    }
}
