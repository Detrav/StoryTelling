using StoryTelling.ViewModels;

namespace StoryTelling.Tests;

public sealed class MetadataTranslationViewModelTests
{
    [Fact]
    public void Constructor_MarksCompleteLanguagesAsDone()
    {
        var viewModel = new MetadataTranslationViewModel(
            ["ru", "de"],
            code => code != "ru",
            (_, _) => Task.CompletedTask);

        Assert.Equal(["ru", "de"], viewModel.Languages.Select(item => item.LanguageCode));
        Assert.True(viewModel.Languages[0].IsDone);
        Assert.False(viewModel.Languages[1].IsDone);
    }

    [Fact]
    public async Task StartAsync_WithoutLanguages_DoesNotRun()
    {
        var ran = false;
        var viewModel = new MetadataTranslationViewModel([], _ => true, (_, _) =>
        {
            ran = true;
            return Task.CompletedTask;
        });

        await viewModel.StartAsync();

        Assert.False(ran);
        Assert.Equal("No target languages to translate.", viewModel.Status);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task StartAsync_AllComplete_ShortCircuits()
    {
        var ran = false;
        var viewModel = new MetadataTranslationViewModel(["ru"], _ => false, (_, _) =>
        {
            ran = true;
            return Task.CompletedTask;
        });

        await viewModel.StartAsync();

        Assert.False(ran);
        Assert.Equal("Book metadata is already up to date.", viewModel.Status);
        Assert.Equal(100, viewModel.Progress);
        Assert.Equal("1 / 1", viewModel.ProgressText);
    }

    [Fact]
    public async Task StartAsync_Success_MarksAllDone()
    {
        var viewModel = new MetadataTranslationViewModel(
            ["ru", "de"],
            _ => true,
            (progress, _) =>
            {
                progress.Report(new MetadataTranslationProgress(0, 2, 0, "Translating…"));
                progress.Report(new MetadataTranslationProgress(1, 2, 1, "Translating…"));
                progress.Report(new MetadataTranslationProgress(2, 2, -1, string.Empty));
                return Task.CompletedTask;
            });

        await viewModel.StartAsync();

        Assert.Equal("Done.", viewModel.Status);
        Assert.Equal(100, viewModel.Progress);
        Assert.Equal("2 / 2", viewModel.ProgressText);
        Assert.All(viewModel.Languages, item => Assert.True(item.IsDone));
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task StartAsync_LateProgressReportAfterFinish_DoesNotRewindTheResult()
    {
        IProgress<MetadataTranslationProgress>? captured = null;
        var viewModel = new MetadataTranslationViewModel(
            ["ru"],
            _ => true,
            (progress, _) =>
            {
                captured = progress;
                progress.Report(new MetadataTranslationProgress(0, 1, 0, "Translating…"));
                return Task.CompletedTask;
            });

        await viewModel.StartAsync();
        Assert.Equal("Done.", viewModel.Status);

        captured!.Report(new MetadataTranslationProgress(0, 1, 0, "Translating…"));

        Assert.Equal("Done.", viewModel.Status);
        Assert.Equal(100, viewModel.Progress);
        Assert.Equal("1 / 1", viewModel.ProgressText);
        Assert.True(Assert.Single(viewModel.Languages).IsDone);
    }

    [Fact]
    public async Task StartAsync_RefusedWhileBusy_FailsTheRunningRow()
    {
        var viewModel = new MetadataTranslationViewModel(
            ["ru"],
            _ => true,
            (progress, _) =>
            {
                progress.Report(new MetadataTranslationProgress(0, 1, 0, "Translating…"));
                return Task.FromException(new InvalidOperationException("Another operation is in progress."));
            });

        await viewModel.StartAsync();

        var item = Assert.Single(viewModel.Languages);
        Assert.Equal("Another operation is in progress.", viewModel.Status);
        Assert.Equal("✗", item.Marker);
        Assert.False(item.IsRunning);
    }

    [Fact]
    public async Task StartAsync_RunnerReportsBusy_SurfacesTheMessage()
    {
        var viewModel = new MetadataTranslationViewModel(
            ["ru"],
            _ => true,
            (_, _) => Task.FromException(new InvalidOperationException("Another operation is in progress. Wait for it to finish.")));

        await viewModel.StartAsync();

        Assert.Equal("Another operation is in progress. Wait for it to finish.", viewModel.Status);
        Assert.False(Assert.Single(viewModel.Languages).IsDone);
    }

    [Fact]
    public async Task StartAsync_Failure_MarksRunningItemFailed()
    {
        var viewModel = new MetadataTranslationViewModel(
            ["ru"],
            _ => true,
            (progress, _) =>
            {
                progress.Report(new MetadataTranslationProgress(0, 1, 0, "Translating…"));
                return Task.FromException(new Exception("Provider unavailable"));
            });

        await viewModel.StartAsync();

        var item = Assert.Single(viewModel.Languages);
        Assert.Equal("Failed: Provider unavailable", viewModel.Status);
        Assert.Equal("✗", item.Marker);
        Assert.Equal("Provider unavailable", item.Stage);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task StartAsync_Cancellation_ResetsRunningItem()
    {
        var viewModel = new MetadataTranslationViewModel(
            ["ru"],
            _ => true,
            (progress, token) =>
            {
                progress.Report(new MetadataTranslationProgress(0, 1, 0, "Translating…"));
                return Task.FromCanceled(new CancellationToken(true));
            });

        await viewModel.StartAsync();

        var item = Assert.Single(viewModel.Languages);
        Assert.Equal("Cancelled.", viewModel.Status);
        Assert.Equal("○", item.Marker);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public void Cancel_CancelsAndRequestsClose()
    {
        var closed = false;
        var viewModel = new MetadataTranslationViewModel(["ru"], _ => false, (_, _) => Task.CompletedTask);
        viewModel.CloseRequested += () => closed = true;
        viewModel.CancelWork();

        viewModel.CancelCommand.Execute(null);

        Assert.True(closed);
    }
}
