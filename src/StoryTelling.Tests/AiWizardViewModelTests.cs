using StoryTelling.Application.Generation;
using StoryTelling.ViewModels;

namespace StoryTelling.Tests;

public sealed class AiWizardViewModelTests
{
    [Fact]
    public void Result_ReturnsSelectedOptionFieldsForGroup()
    {
        var viewModel = new AiWizardViewModel("World", GenerationTarget.World, Options(
            new GenerationOption(new Dictionary<string, string>
            {
                ["WorldTitle"] = "T",
                ["WorldBody"] = "B",
            })));

        Assert.NotNull(viewModel.SelectedOption);
        Assert.False(viewModel.AllowEdits);
        Assert.Equal("B", viewModel.Result!["WorldBody"]);
        Assert.Equal("T\n\nB", viewModel.SelectedOption!.Display);
    }

    [Fact]
    public void Result_EditsOverrideSingleField()
    {
        var viewModel = new AiWizardViewModel("Book", GenerationTarget.ProjectName, Options());

        viewModel.Edits = "  custom  ";

        Assert.True(viewModel.AllowEdits);
        Assert.Equal("custom", viewModel.Result!["ProjectName"]);
    }

    [Fact]
    public void MoreOptions_AppendsInsteadOfReplacing()
    {
        var viewModel = new AiWizardViewModel("World", GenerationTarget.World, Options(
            new GenerationOption(new Dictionary<string, string> { ["WorldTitle"] = "T", ["WorldBody"] = "B" })));

        Assert.Single(viewModel.Options);

        viewModel.MoreOptionsCommand.Execute(null);

        Assert.Equal(2, viewModel.Options.Count);
    }

    [Fact]
    public async Task Stop_CancelsGeneration()
    {
        var viewModel = new AiWizardViewModel("World", GenerationTarget.World, (_, _, _, _, token) =>
        {
            var completion = new TaskCompletionSource<IReadOnlyList<GenerationOption>>();
            token.Register(() => completion.SetCanceled(token));
            return completion.Task;
        });

        viewModel.StopCommand.Execute(null);
        await viewModel.Initialization;

        Assert.Equal("Stopped.", viewModel.Status);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public void OptionCount_PersistsAcrossInstances()
    {
        var first = new AiWizardViewModel("World", GenerationTarget.World, Options());
        first.OptionCount = 5;

        var second = new AiWizardViewModel("World", GenerationTarget.World, Options());

        Assert.Equal(5, second.OptionCount);

        AiWizardViewModel.LastOptionCount = 3;
    }

    private static AiWizardViewModel.GenerateOptions Options(params GenerationOption[] options) =>
        (_, _, _, _, _) => Task.FromResult<IReadOnlyList<GenerationOption>>(options);
}
