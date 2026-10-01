using System.Collections.Generic;
using System.Linq;
using StoryTelling.ViewModels;

namespace StoryTelling.Tests;

public sealed class BookCompletionViewModelTests
{
    [Fact]
    public void Constructor_BuildsOneRowPerOperation()
    {
        var viewModel = Build([new BookOperation(BookOperationKind.WriteChapter, 1, "Write chapter 1")]);

        var item = Assert.Single(viewModel.Operations);
        Assert.Equal("Write chapter 1", item.Header);
        Assert.Equal("○", item.Marker);
    }

    [Fact]
    public void Constructor_MarksSkippedOperations()
    {
        var viewModel = Build([new BookOperation(BookOperationKind.Skip, 2, "Chapter 2 — skipped", SkipReason: "no direction")]);

        var item = Assert.Single(viewModel.Operations);
        Assert.Equal("⚠", item.Marker);
        Assert.Equal("no direction", item.Stage);
    }

    [Fact]
    public async Task StartAsync_EmptyPlan_ReportsEverythingUpToDate()
    {
        var ran = false;
        var viewModel = new BookCompletionViewModel(
            () => [],
            (_, _, _) =>
            {
                ran = true;
                return Task.CompletedTask;
            });

        await viewModel.StartAsync();

        Assert.False(ran);
        Assert.Equal("Everything is already up to date.", viewModel.Status);
        Assert.True(viewModel.IsFinished);
    }

    [Fact]
    public async Task StartAsync_Success_MarksAllDone()
    {
        var viewModel = Build([Operation(1), Operation(2)], (_, progress, _) =>
        {
            progress.Report(new BookCompletionProgress(0, 2, 0, "Writing…"));
            progress.Report(new BookCompletionProgress(1, 2, 1, "Writing…"));
            progress.Report(new BookCompletionProgress(2, 2, -1, string.Empty));
            return Task.CompletedTask;
        });

        await viewModel.StartAsync();

        Assert.Equal("Done.", viewModel.Status);
        Assert.Equal(100, viewModel.Progress);
        Assert.Equal("2 / 2", viewModel.ProgressText);
        Assert.All(viewModel.Operations, item =>
        {
            Assert.False(item.IsRunning);
            Assert.Empty(item.Stage);
        });
    }

    [Fact]
    public async Task StartAsync_LateProgressReportAfterFinish_DoesNotRewindTheResult()
    {
        IProgress<BookCompletionProgress>? captured = null;
        var viewModel = Build([Operation(1)], (_, progress, _) =>
        {
            captured = progress;
            progress.Report(new BookCompletionProgress(0, 1, 0, "Writing…"));
            return Task.CompletedTask;
        });

        await viewModel.StartAsync();
        Assert.Equal("Done.", viewModel.Status);

        captured!.Report(new BookCompletionProgress(0, 1, 0, "Writing…"));

        Assert.Equal("Done.", viewModel.Status);
        Assert.Equal(100, viewModel.Progress);
        Assert.Equal("1 / 1", viewModel.ProgressText);
        var finished = Assert.Single(viewModel.Operations);
        Assert.False(finished.IsRunning);
        Assert.Empty(finished.Stage);
    }

    [Fact]
    public async Task StartAsync_RefusedWhileBusy_ShowsTheMessageAndFailsTheRunningRow()
    {
        var viewModel = Build([Operation(1)], (_, progress, _) =>
        {
            progress.Report(new BookCompletionProgress(0, 1, 0, "Writing…"));
            return Task.FromException(new InvalidOperationException("Another operation is in progress."));
        });

        await viewModel.StartAsync();

        var item = Assert.Single(viewModel.Operations);
        Assert.Equal("Another operation is in progress.", viewModel.Status);
        Assert.Equal("✗", item.Marker);
        Assert.False(item.IsRunning);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task StartAsync_Failure_MarksRunningItemFailed()
    {
        var viewModel = Build([Operation(1)], (_, progress, _) =>
        {
            progress.Report(new BookCompletionProgress(0, 1, 0, "Writing…"));
            return Task.FromException(new Exception("Provider unavailable"));
        });

        await viewModel.StartAsync();

        var item = Assert.Single(viewModel.Operations);
        Assert.Equal("Failed: Provider unavailable", viewModel.Status);
        Assert.Equal("✗", item.Marker);
        Assert.Equal("Provider unavailable", item.Stage);
    }

    [Fact]
    public async Task StartAsync_Cancellation_ResetsRunningItem()
    {
        var viewModel = Build([Operation(1)], (_, progress, _) =>
        {
            progress.Report(new BookCompletionProgress(0, 1, 0, "Writing…"));
            return Task.FromCanceled(new CancellationToken(true));
        });

        await viewModel.StartAsync();

        var item = Assert.Single(viewModel.Operations);
        Assert.Equal("Cancelled.", viewModel.Status);
        Assert.Equal("○", item.Marker);
        Assert.False(item.IsRunning);
    }

    private static BookOperation Operation(int number) =>
        new(BookOperationKind.WriteChapter, number, $"Write chapter {number}");

    private static BookCompletionViewModel Build(
        IReadOnlyList<BookOperation> operations,
        BookCompletionViewModel.CompletionRunner? run = null)
    {
        var list = operations.ToList();
        return new BookCompletionViewModel(
            () => list,
            run ?? ((_, _, _) => Task.CompletedTask));
    }
}
