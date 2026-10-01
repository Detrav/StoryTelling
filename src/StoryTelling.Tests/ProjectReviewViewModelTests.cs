using StoryTelling.Application.Generation;
using StoryTelling.Application.Review;
using StoryTelling.ViewModels;

namespace StoryTelling.Tests;

public sealed class ProjectReviewViewModelTests
{
    [Fact]
    public void Run_PopulatesFindings()
    {
        var finding = new ReviewFinding(ReviewSeverity.Error, ReviewArea.Knowledge, "Gap", "No world description.", null);
        var viewModel = new ProjectReviewViewModel((_, _, _) => Task.FromResult<IReadOnlyList<ReviewFinding>>([finding]), new FakeReviewFixHost());

        Assert.Empty(viewModel.Findings);

        viewModel.RunCommand.Execute(null);

        Assert.Single(viewModel.Findings);
        Assert.Equal("1 findings.", viewModel.Status);
    }

    [Fact]
    public void ApplyFix_MarksFindingFixedAndSignalsHost()
    {
        var fix = new ReviewFix([new ReviewEdit(GenerationTarget.Knowledge, "Ashen Reach", "Content", "new")]);
        var finding = new ReviewFinding(ReviewSeverity.Warning, ReviewArea.Knowledge, "Tone vs rating", "detail", null, fix, "Ashen Reach");
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
        var knowledge = new ReviewFinding(ReviewSeverity.Info, ReviewArea.Knowledge, "T", "d", null, null, "Ashen Reach");
        var knowledgeWithout = new ReviewFinding(ReviewSeverity.Info, ReviewArea.Knowledge, "T", "d", null, null, null);
        var general = new ReviewFinding(ReviewSeverity.Info, ReviewArea.General, "T", "d", null, null, null);

        Assert.Equal(GenerationTarget.Knowledge, new ReviewFindingViewModel(knowledge).AiTarget);
        Assert.Null(new ReviewFindingViewModel(knowledgeWithout).AiTarget);
        Assert.Null(new ReviewFindingViewModel(general).AiTarget);
    }
}
