using StoryTelling.Application.Generation;
using StoryTelling.Application.Review;
using StoryTelling.Domain;
using StoryTelling.ViewModels;

namespace StoryTelling.Tests;

public sealed class ProjectReviewViewModelTests
{
    private static ReviewCheck Check(string id) => new(id, id, ReviewScope.Project, "intro", ["a"], MaxChars: 1000);

    private static ProjectReviewViewModel Build(ProjectReviewViewModel.RunCheck run, FakeReviewFixHost host, params ReviewCheck[] checks)
    {
        var list = checks.Length == 0 ? [Check("one")] : checks;
        return new ProjectReviewViewModel(list, run, host);
    }

    [Fact]
    public async Task Start_RunsFirstStep()
    {
        var finding = new ReviewFinding(ReviewSeverity.Error, ReviewArea.Knowledge, "Gap", "No world description.", null);
        var viewModel = Build((_, _, _, _) => Task.FromResult<IReadOnlyList<ReviewFinding>>([finding]), new FakeReviewFixHost());

        await viewModel.StartAsync();

        var step = viewModel.CurrentStep!;
        Assert.True(step.WasRun);
        Assert.Single(step.Findings);
        Assert.Equal("1 finding(s).", step.Status);
    }

    [Fact]
    public async Task Next_AdvancesToTheNextCheck()
    {
        var viewModel = Build((_, _, _, _) => Task.FromResult<IReadOnlyList<ReviewFinding>>([]), new FakeReviewFixHost(), Check("one"), Check("two"));

        await viewModel.StartAsync();
        Assert.Equal("one", viewModel.CurrentStep!.Label);

        await viewModel.NextCommand.ExecuteAsync(null);

        Assert.Equal("two", viewModel.CurrentStep!.Label);
        Assert.Equal(1, viewModel.StepIndex);
    }

    [Fact]
    public async Task Next_OnLastStep_Finishes()
    {
        var viewModel = Build((_, _, _, _) => Task.FromResult<IReadOnlyList<ReviewFinding>>([]), new FakeReviewFixHost());

        await viewModel.StartAsync();
        await viewModel.NextCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsFinished);
        Assert.Null(viewModel.CurrentStep);
        Assert.Contains("experts have reported", viewModel.Status);
    }

    [Fact]
    public async Task Back_ReturnsToPreviousStepWithoutRerunning()
    {
        var calls = 0;
        var viewModel = Build((_, _, _, _) =>
        {
            calls++;
            return Task.FromResult<IReadOnlyList<ReviewFinding>>([]);
        }, new FakeReviewFixHost(), Check("one"), Check("two"));

        await viewModel.StartAsync();
        await viewModel.NextCommand.ExecuteAsync(null);
        viewModel.BackCommand.Execute(null);

        Assert.Equal("one", viewModel.CurrentStep!.Label);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task ApplyFix_MarksStaleWhenSignatureChanges()
    {
        var fix = new ReviewFix([new ReviewEdit(GenerationTarget.Knowledge, "Ashen Reach", "Content", "new")]);
        var finding = new ReviewFinding(ReviewSeverity.Warning, ReviewArea.Knowledge, "Tone vs rating", "detail", null, fix, "Ashen Reach");
        var host = new FakeReviewFixHost { Signature = "before" };
        var viewModel = Build((_, _, _, _) => Task.FromResult<IReadOnlyList<ReviewFinding>>([finding]), host);

        await viewModel.StartAsync();
        var step = viewModel.CurrentStep!;
        Assert.False(step.IsStale);

        host.Signature = "after";
        viewModel.ApplyFix(step.Findings[0], fix);

        Assert.True(step.IsStale);
    }

    [Fact]
    public async Task Skip_MovesPastTheStep()
    {
        var calls = 0;
        var viewModel = Build((_, _, _, _) =>
        {
            calls++;
            return Task.FromResult<IReadOnlyList<ReviewFinding>>([]);
        }, new FakeReviewFixHost(), Check("one"), Check("two"));

        await viewModel.StartAsync();
        await viewModel.SkipCommand.ExecuteAsync(null);

        Assert.Equal("two", viewModel.CurrentStep!.Label);
        Assert.Equal(2, calls);
        Assert.True(viewModel.Steps[0].IsSkipped);
    }

    [Fact]
    public async Task AddEntry_DelegatesToHostAndMarksStale()
    {
        var host = new FakeReviewFixHost { Signature = "before" };
        var viewModel = Build((_, _, _, _) => Task.FromResult<IReadOnlyList<ReviewFinding>>([]), host);
        await viewModel.StartAsync();

        host.Signature = "after";
        var entry = new KnowledgeEntry { Kind = KnowledgeKind.Character, Title = "Captain Thorne" };
        viewModel.AddEntry(entry, "Create: Captain Thorne");

        Assert.Same(entry, host.LastAdded);
        Assert.Equal("Create: Captain Thorne", host.LastLabel);
        Assert.True(viewModel.Steps[0].IsStale);
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
