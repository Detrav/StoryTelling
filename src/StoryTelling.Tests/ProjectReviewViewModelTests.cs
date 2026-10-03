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

    private static ReviewChange Change(ReviewEdit edit, string label, string oldValue, string newValue) =>
        new(edit, label, oldValue, newValue);

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
    public async Task DeterministicFix_IsPreparedAutomatically()
    {
        var edit = new ReviewEdit(GenerationTarget.Knowledge, "Ashen Reach", "Content", "new");
        var fix = new ReviewFix([edit]);
        var finding = new ReviewFinding(ReviewSeverity.Warning, ReviewArea.Knowledge, "Tone vs rating", "detail", null, fix, "Ashen Reach");
        var host = new FakeReviewFixHost { Changes = [Change(edit, "Ashen Reach · Content", "old", "new")] };
        var viewModel = Build((_, _, _, _) => Task.FromResult<IReadOnlyList<ReviewFinding>>([finding]), host);

        await viewModel.StartAsync();

        var prepared = viewModel.CurrentStep!.Findings[0];
        Assert.True(prepared.IsPrepared);
        Assert.Single(prepared.Rows);
        Assert.True(prepared.CanFix);
        Assert.Equal("Apply", prepared.FixLabel);
    }

    [Fact]
    public async Task ApplyFix_AppliesSelectedEditsAndMarksStale()
    {
        var edit = new ReviewEdit(GenerationTarget.Knowledge, "Ashen Reach", "Content", "new");
        var fix = new ReviewFix([edit]);
        var finding = new ReviewFinding(ReviewSeverity.Warning, ReviewArea.Knowledge, "Tone vs rating", "detail", null, fix, "Ashen Reach");
        var host = new FakeReviewFixHost { Signature = "before", Changes = [Change(edit, "Ashen Reach · Content", "old", "new")] };
        var viewModel = Build((_, _, _, _) => Task.FromResult<IReadOnlyList<ReviewFinding>>([finding]), host);

        await viewModel.StartAsync();
        var step = viewModel.CurrentStep!;
        var prepared = step.Findings[0];

        host.Signature = "after";
        await prepared.ApplyAsync();

        Assert.True(prepared.IsApplied);
        Assert.Single(host.LastApplied!.Edits);
        Assert.Equal("new", host.LastApplied!.Edits[0].Value);
        Assert.True(step.IsStale);
    }

    [Fact]
    public async Task CreateEntryFinding_AddsEntryAndMarksStale()
    {
        var host = new FakeReviewFixHost { Signature = "before" };
        var finding = new ReviewFinding(ReviewSeverity.Warning, ReviewArea.Knowledge, "Missing entry for Dmitri", "Dmitri has no knowledge entry.", null, null, "Dmitri");
        var viewModel = Build((_, _, _, _) => Task.FromResult<IReadOnlyList<ReviewFinding>>([finding]), host);

        await viewModel.StartAsync();
        var prepared = viewModel.CurrentStep!.Findings[0];
        Assert.Equal(ReviewFindingAction.CreateEntry, prepared.Action);
        Assert.True(prepared.IsPrepared);

        host.Signature = "after";
        await prepared.ApplyAsync();

        Assert.NotNull(host.LastAdded);
        Assert.Equal("Dmitri", host.LastAdded!.Title);
        Assert.True(viewModel.Steps[0].IsStale);
    }

    [Fact]
    public async Task SelectAllAndClear_TogglesFindingSelection()
    {
        var edit = new ReviewEdit(GenerationTarget.Knowledge, "Ashen Reach", "Content", "new");
        var fix = new ReviewFix([edit]);
        var finding = new ReviewFinding(ReviewSeverity.Warning, ReviewArea.Knowledge, "Tone", "detail", null, fix, "Ashen Reach");
        var host = new FakeReviewFixHost { Changes = [Change(edit, "Ashen Reach · Content", "old", "new")] };
        var viewModel = Build((_, _, _, _) => Task.FromResult<IReadOnlyList<ReviewFinding>>([finding]), host);

        await viewModel.StartAsync();

        viewModel.ClearFindingSelectionCommand.Execute(null);
        Assert.All(viewModel.CurrentStep!.Findings, candidate => Assert.False(candidate.IsSelected));
        Assert.False(viewModel.CanApplySelected);

        viewModel.SelectAllFindingsCommand.Execute(null);
        Assert.All(viewModel.CurrentStep!.Findings, candidate => Assert.True(candidate.IsSelected));
        Assert.True(viewModel.CanApplySelected);
    }

    [Fact]
    public async Task ApplySelected_AppliesEverySelectedFinding()
    {
        var edit = new ReviewEdit(GenerationTarget.Knowledge, "Ashen Reach", "Content", "new");
        var fix = new ReviewFix([edit]);
        var finding = new ReviewFinding(ReviewSeverity.Warning, ReviewArea.Knowledge, "Tone", "detail", null, fix, "Ashen Reach");
        var host = new FakeReviewFixHost { Changes = [Change(edit, "Ashen Reach · Content", "old", "new")] };
        var viewModel = Build((_, _, _, _) => Task.FromResult<IReadOnlyList<ReviewFinding>>([finding, finding]), host);

        await viewModel.StartAsync();
        await viewModel.ApplySelectedCommand.ExecuteAsync(null);

        Assert.Equal(2, host.ApplyCount);
        Assert.All(viewModel.CurrentStep!.Findings, candidate => Assert.True(candidate.IsApplied));
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
