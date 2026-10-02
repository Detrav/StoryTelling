using System.Threading;
using StoryTelling.ViewModels;

namespace StoryTelling.Tests;

public sealed class ProgressTaskViewModelTests
{
    [Fact]
    public async Task StartAsync_Success_MarksEveryItemDone()
    {
        var viewModel = Build("Plan the final chapter", (_, _) => Task.CompletedTask);

        await viewModel.StartAsync();

        Assert.Equal("Done.", viewModel.Status);
        Assert.False(viewModel.IsBusy);
        Assert.True(Assert.Single(viewModel.Items).IsDone);
    }

    [Fact]
    public void HasProgress_IsTrueOnlyForMultiStepPlans()
    {
        var single = Build("One", (_, _) => Task.CompletedTask);
        var multi = new ProgressTaskViewModel(
            "Two",
            "Two steps",
            [new ProgressItemViewModel("A"), new ProgressItemViewModel("B")],
            (_, _) => Task.CompletedTask);

        Assert.False(single.HasProgress);
        Assert.True(multi.HasProgress);
        Assert.True(multi.HasDescription);
    }

    [Fact]
    public async Task StartAsync_Failure_ShowsTheMessageAndKeepsTheItemUndone()
    {
        var viewModel = Build("Plan the final chapter", (_, _) =>
            Task.FromException(new InvalidOperationException("The model returned no usable plan.")));

        await viewModel.StartAsync();

        Assert.Equal("Failed: The model returned no usable plan.", viewModel.Status);
        Assert.False(viewModel.IsBusy);
        Assert.False(Assert.Single(viewModel.Items).IsDone);
    }

    [Fact]
    public async Task Cancel_StopsTheRunnerAndReportsCancelled()
    {
        var viewModel = Build("Plan the final chapter", async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
        });

        var closing = false;
        viewModel.CloseRequested += () => closing = true;

        var start = viewModel.StartAsync();
        await Task.Delay(30);
        viewModel.CancelCommand.Execute(null);
        await start;

        Assert.True(closing);
        Assert.Equal("Cancelled.", viewModel.Status);
        Assert.False(viewModel.IsBusy);
    }

    private static ProgressTaskViewModel Build(string header, ProgressTaskViewModel.Runner run) =>
        new("Finish story", "Description", [new ProgressItemViewModel(header)], run);
}
