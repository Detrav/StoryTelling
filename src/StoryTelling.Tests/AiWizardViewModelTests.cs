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

    private static AiWizardViewModel.GenerateOptions Options(params GenerationOption[] options) =>
        (_, _) => Task.FromResult<IReadOnlyList<GenerationOption>>(options);
}
