using System.Collections.Generic;

namespace StoryTelling.ViewModels;

public static class ProgressItems
{
    public static void Update<T>(IList<T> items, int completed, int currentIndex, string stage)
        where T : ProgressItemViewModel
    {
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            if (index == currentIndex)
            {
                item.MarkRunning(stage);
            }
            else if (index < completed)
            {
                item.MarkDone();
            }
            else
            {
                item.MarkPending();
            }
        }
    }

    public static void MarkAllDone<T>(IList<T> items)
        where T : ProgressItemViewModel
    {
        foreach (var item in items)
        {
            item.MarkDone();
        }
    }

    public static void CancelRunning<T>(IList<T> items)
        where T : ProgressItemViewModel
    {
        foreach (var item in items)
        {
            if (item.IsRunning)
            {
                item.MarkPending();
            }
        }
    }

    public static void FailRunning<T>(IList<T> items, string message)
        where T : ProgressItemViewModel
    {
        foreach (var item in items)
        {
            if (item.IsRunning)
            {
                item.MarkFailed(message);
            }
        }
    }
}
