using StoryTelling.Application.Generation;
using StoryTelling.Application.Review;
using StoryTelling.ViewModels;

namespace StoryTelling.Tests;

public sealed class ProjectReviewViewModelTests
{
    [Fact]
    public void Run_PopulatesFindings()
    {
        var finding = new ReviewFinding(ReviewSeverity.Error, ReviewArea.World, "Gap", "No world description.", null);
        var viewModel = new ProjectReviewViewModel((_, _, _) => Task.FromResult<IReadOnlyList<ReviewFinding>>([finding]), new FakeReviewFixHost());

        Assert.Empty(viewModel.Findings);

        viewModel.RunCommand.Execute(null);

        Assert.Single(viewModel.Findings);
        Assert.Equal("1 findings.", viewModel.Status);
    }

    [Fact]
    public void ApplyFix_MarksFindingFixedAndSignalsHost()
    {
        var fix = new ReviewFix([new ReviewEdit(GenerationTarget.Frame, string.Empty, "Tone", "hopeful")]);
        var finding = new ReviewFinding(ReviewSeverity.Warning, ReviewArea.Frame, "Tone vs rating", "detail", null, fix, null);
        var host = new FakeReviewFixHost();
        var viewModel = new ProjectReviewViewModel((_, _, _) => Task.FromResult<IReadOnlyList<ReviewFinding>>([finding]), host);

        viewModel.RunCommand.Execute(null);
        var item = viewModel.Findings[0];

        Assert.True(item.CanApplyFix);

        viewModel.ApplyFix(item, fix);

        Assert.True(item.IsFixed);
        Assert.Equal("Fixed", item.FixLabel);
        Assert.Equal("Fix: Tone vs rating", host.LastLabel);
        Assert.Same(fix, host.LastApplied);
    }

    [Fact]
    public void AiTarget_DependsOnAreaAndReference()
    {
        var characters = new ReviewFinding(ReviewSeverity.Info, ReviewArea.Characters, "T", "d", null, null, "Aria");
        var charactersWithout = new ReviewFinding(ReviewSeverity.Info, ReviewArea.Characters, "T", "d", null, null, null);
        var languages = new ReviewFinding(ReviewSeverity.Info, ReviewArea.Languages, "T", "d", null, null, null);

        Assert.Equal(GenerationTarget.Character, new ReviewFindingViewModel(characters).AiTarget);
        Assert.Null(new ReviewFindingViewModel(charactersWithout).AiTarget);
        Assert.Null(new ReviewFindingViewModel(languages).AiTarget);
    }
}
