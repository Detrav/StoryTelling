using StoryTelling.Application.Generation;
using StoryTelling.ViewModels;

namespace StoryTelling.Tests;

public sealed class ChapterGenerationViewModelTests
{
    [Fact]
    public async Task StartAsync_Success_EnablesClosing()
    {
        var viewModel = new ChapterGenerationViewModel((_, _) => Task.CompletedTask);

        await viewModel.StartAsync();

        Assert.Equal("Done.", viewModel.Status);
        Assert.True(viewModel.CanClose);
        Assert.False(viewModel.IsBusy);
        Assert.True(viewModel.HasDetail);
    }

    [Fact]
    public async Task StartAsync_Failure_ShowsTheMessageAndEnablesClosing()
    {
        var viewModel = new ChapterGenerationViewModel((_, _) =>
            Task.FromException(new InvalidOperationException("Another operation is in progress.")));

        await viewModel.StartAsync();

        Assert.Equal("Failed: Another operation is in progress.", viewModel.Status);
        Assert.True(viewModel.CanClose);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public void Steps_ListsTheFourPipelineStages()
    {
        var viewModel = new ChapterGenerationViewModel((_, _) => Task.CompletedTask);

        Assert.Equal(4, viewModel.Steps.Count);
        Assert.All(viewModel.Steps, step =>
        {
            Assert.False(step.IsDone);
            Assert.False(step.IsRunning);
        });
    }

    [Theory]
    [InlineData("Gathering context", -1, 0)]
    [InlineData("Gathering context", 0, 0)]
    [InlineData("Writing", 0, 1)]
    [InlineData("Gathering context", 1, 2)]
    [InlineData("Editing", 2, 2)]
    [InlineData("Summarizing", 2, 3)]
    [InlineData("Completing", 1, 1)]
    public void MapStep_MapsThePipelineStageToItsStep(string stage, int current, int expected)
    {
        Assert.Equal(expected, ChapterGenerationViewModel.MapStep(stage, current));
    }

    [Fact]
    public async Task StartAsync_Success_MarksEveryStepDone()
    {
        var viewModel = new ChapterGenerationViewModel((_, _) => Task.CompletedTask);

        await viewModel.StartAsync();

        Assert.All(viewModel.Steps, step => Assert.True(step.IsDone));
    }

    [Fact]
    public async Task StartAsync_LateProgressReportAfterFinish_DoesNotRewindTheStatus()
    {
        IProgress<GenerationProgress>? captured = null;
        var viewModel = new ChapterGenerationViewModel((progress, _) =>
        {
            captured = progress;
            return Task.CompletedTask;
        });

        await viewModel.StartAsync();
        Assert.Equal("Done.", viewModel.Status);

        captured!.Report(new GenerationProgress("Writing", 1));

        Assert.Equal("Done.", viewModel.Status);
    }

    [Fact]
    public void CloseCommand_RaisesCloseRequested()
    {
        var closed = false;
        var viewModel = new ChapterGenerationViewModel((_, _) => Task.CompletedTask);
        viewModel.CloseRequested += () => closed = true;

        viewModel.CloseCommand.Execute(null);

        Assert.True(closed);
    }

    [Fact]
    public async Task Cancel_StopsTheRunnerAndReportsCancelled()
    {
        var started = new TaskCompletionSource();
        var viewModel = new ChapterGenerationViewModel(async (_, token) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, token);
        });

        var run = viewModel.StartAsync();
        await started.Task;

        viewModel.CancelCommand.Execute(null);
        await run;

        Assert.Equal("Cancelled.", viewModel.Status);
        Assert.True(viewModel.CanClose);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public void CancelCommand_RaisesCloseRequested()
    {
        var closed = false;
        var viewModel = new ChapterGenerationViewModel((_, _) => Task.CompletedTask);
        viewModel.CloseRequested += () => closed = true;

        viewModel.CancelCommand.Execute(null);

        Assert.True(closed);
    }

    [Fact]
    public async Task CancelWork_CancelsTheSuppliedToken()
    {
        CancellationToken captured = default;
        var started = new TaskCompletionSource();
        var viewModel = new ChapterGenerationViewModel(async (_, token) =>
        {
            captured = token;
            started.SetResult();
            await Task.Delay(Timeout.Infinite, token);
        });

        var run = viewModel.StartAsync();
        await started.Task;

        viewModel.CancelWork();
        await run;

        Assert.True(captured.IsCancellationRequested);
        Assert.Equal("Cancelled.", viewModel.Status);
    }
}
