using StoryTelling.Application.Settings;
using StoryTelling.Infrastructure.Diff;
using StoryTelling.ViewModels;

namespace StoryTelling.Tests;

public sealed class SettingsWindowViewModelTests
{
    [Fact]
    public void BuildSettings_CarriesTheContextBudgets()
    {
        var viewModel = new SettingsWindowViewModel(new DiffPlexTextDiff(), new AppSettings(), new FakeLlmClient("ignored"));

        viewModel.ContextTokenBudget = 5000;
        viewModel.RecentLoglineCount = 4;
        viewModel.ContextRequiredSectionMaxChars = 9000;
        viewModel.ToolResultMaxChars = 48000;
        viewModel.StorySoFarMode = "Loglines";

        var built = viewModel.BuildSettings();

        Assert.Equal(5000, built.ContextTokenBudget);
        Assert.Equal(4, built.RecentLoglineCount);
        Assert.Equal(9000, built.ContextRequiredSectionMaxChars);
        Assert.Equal(48000, built.ToolResultMaxChars);
        Assert.Equal("Loglines", built.StorySoFarMode);
    }

    [Fact]
    public async Task ContextBudgets_RoundTripThroughUndo()
    {
        var viewModel = new SettingsWindowViewModel(new DiffPlexTextDiff(), new AppSettings(), new FakeLlmClient("ignored"));
        viewModel.ContextTokenBudget = 1000;
        viewModel.RecentLoglineCount = 1;
        viewModel.ContextRequiredSectionMaxChars = 1500;
        viewModel.ToolResultMaxChars = 2000;
        viewModel.Commit();

        await viewModel.UndoCommand.ExecuteAsync(null);

        Assert.Equal(AppSettings.CreateDefault().ContextTokenBudget, viewModel.ContextTokenBudget);
        Assert.Equal(AppSettings.CreateDefault().RecentLoglineCount, viewModel.RecentLoglineCount);
        Assert.Equal(AppSettings.CreateDefault().ContextRequiredSectionMaxChars, viewModel.ContextRequiredSectionMaxChars);
        Assert.Equal(AppSettings.CreateDefault().ToolResultMaxChars, viewModel.ToolResultMaxChars);
    }
}
