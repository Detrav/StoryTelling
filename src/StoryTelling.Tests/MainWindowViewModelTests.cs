using Microsoft.Extensions.Logging.Abstractions;
using StoryTelling.Application.Chapters;
using StoryTelling.Application.Generation;
using StoryTelling.Application.Knowledge;
using StoryTelling.Application.Review;
using StoryTelling.Application.Translation;
using StoryTelling.Infrastructure;
using StoryTelling.ViewModels;

namespace StoryTelling.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public async Task ConfirmClose_WithoutProject_NeedsNoPrompt()
    {
        var viewModel = Build();
        var prompted = false;
        viewModel.SaveBeforeCloseRequested += _ =>
        {
            prompted = true;
            return Task.FromResult(true);
        };

        Assert.True(await viewModel.ConfirmCloseAsync());
        Assert.False(prompted);
    }

    [Fact]
    public async Task ConfirmClose_CleanProject_NeedsNoPrompt()
    {
        var viewModel = Build();
        viewModel.NewProjectCommand.Execute(null);
        viewModel.Workspace!.IsDirty = false;
        var prompted = false;
        viewModel.SaveBeforeCloseRequested += _ =>
        {
            prompted = true;
            return Task.FromResult(true);
        };

        Assert.True(await viewModel.ConfirmCloseAsync());
        Assert.False(prompted);
    }

    [Fact]
    public async Task ConfirmClose_DirtyWithoutHandler_Aborts()
    {
        var viewModel = Build();
        viewModel.NewProjectCommand.Execute(null);
        viewModel.Workspace!.IsDirty = true;

        Assert.False(await viewModel.ConfirmCloseAsync());
    }

    [Fact]
    public async Task ConfirmClose_DirtyAndAccepted_Proceeds()
    {
        var viewModel = Build();
        viewModel.NewProjectCommand.Execute(null);
        viewModel.Workspace!.IsDirty = true;
        viewModel.SaveBeforeCloseRequested += _ => Task.FromResult(true);

        Assert.True(await viewModel.ConfirmCloseAsync());
    }

    [Fact]
    public async Task ConfirmClose_DirtyAndDeclined_Aborts()
    {
        var viewModel = Build();
        viewModel.NewProjectCommand.Execute(null);
        viewModel.Workspace!.IsDirty = true;
        viewModel.SaveBeforeCloseRequested += _ => Task.FromResult(false);

        Assert.False(await viewModel.ConfirmCloseAsync());
    }

    [Fact]
    public async Task CloseProject_DirtyAndDeclined_KeepsTheProjectOpen()
    {
        var viewModel = Build();
        viewModel.NewProjectCommand.Execute(null);
        viewModel.Workspace!.IsDirty = true;
        viewModel.SaveBeforeCloseRequested += _ => Task.FromResult(false);

        await viewModel.CloseProjectCommand.ExecuteAsync(null);

        Assert.NotNull(viewModel.Workspace);
    }

    [Fact]
    public async Task CloseProject_DirtyAndAccepted_ShowsTheWelcomeState()
    {
        var viewModel = Build();
        viewModel.NewProjectCommand.Execute(null);
        viewModel.Workspace!.IsDirty = true;
        viewModel.SaveBeforeCloseRequested += _ => Task.FromResult(true);

        await viewModel.CloseProjectCommand.ExecuteAsync(null);

        Assert.Null(viewModel.Workspace);
        Assert.IsType<WelcomeViewModel>(viewModel.Content);
    }

    private static MainWindowViewModel Build() => new(
        new JsonProjectRepository(),
        new FakeSettingsService(),
        new FakeClock(DateTimeOffset.UnixEpoch),
        new Infrastructure.Diff.DiffPlexTextDiff(),
        new FakeLlmClient("pong"),
        new FakeGenerationAssistant(),
        new FakeKnowledgeImporter(),
        new FakeProjectReviewAssistant(),
        new FakeChapterRunner(),
        new FakeTranslationService(),
        new FakeMetadataTranslator(),
        NullLogger<MainWindowViewModel>.Instance);
}
