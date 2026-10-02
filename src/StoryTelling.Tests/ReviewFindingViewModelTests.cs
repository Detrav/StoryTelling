using StoryTelling.Application.Generation;
using StoryTelling.Application.Review;
using StoryTelling.ViewModels;

namespace StoryTelling.Tests;

public sealed class ReviewFindingViewModelTests
{
    private static ReviewFinding Finding(string title, string detail, ReviewFix? fix = null, string? reference = null) =>
        new(ReviewSeverity.Warning, ReviewArea.Knowledge, title, detail, null, fix, reference);

    [Fact]
    public void WithFix_ShowsFixOnly()
    {
        var fix = new ReviewFix([new ReviewEdit(GenerationTarget.Knowledge, "Morozov", "Tags", "52 years old")]);
        var viewModel = new ReviewFindingViewModel(Finding("Age mismatch", "tag vs body", fix, "Morozov"));

        Assert.Equal(ReviewFindingAction.Fix, viewModel.Action);
        Assert.True(viewModel.CanApplyFix);
        Assert.False(viewModel.CanFixWithAi);
        Assert.False(viewModel.CanCreateEntry);
    }

    [Fact]
    public void MissingEntryWithoutFix_ShowsCreateEntry()
    {
        var viewModel = new ReviewFindingViewModel(Finding("Missing entry for Dmitri Volkov", "Dmitri has no knowledge entry."));

        Assert.Equal(ReviewFindingAction.CreateEntry, viewModel.Action);
        Assert.True(viewModel.CanCreateEntry);
        Assert.False(viewModel.CanFixWithAi);
    }

    [Fact]
    public void DanglingReference_ShowsCreateEntry()
    {
        var viewModel = new ReviewFindingViewModel(Finding("Dangling reference to Captain Thorne", "Referenced but never defined."));

        Assert.Equal(ReviewFindingAction.CreateEntry, viewModel.Action);
    }

    [Fact]
    public void SuggestionStartingWithCreate_ShowsCreateEntry()
    {
        var finding = new ReviewFinding(ReviewSeverity.Warning, ReviewArea.Knowledge, "Group unnamed", "The Weavers has no scope.", "Create Faction: The Weavers");

        Assert.Equal(ReviewFindingAction.CreateEntry, new ReviewFindingViewModel(finding).Action);
    }

    [Fact]
    public void GenericIssueWithoutFix_ShowsFixWithAi()
    {
        var viewModel = new ReviewFindingViewModel(Finding("Timeline anchor unclear", "The backstory is not anchored."));

        Assert.Equal(ReviewFindingAction.FixWithAi, viewModel.Action);
        Assert.True(viewModel.CanFixWithAi);
        Assert.False(viewModel.CanCreateEntry);
    }

    [Fact]
    public void GenericIssueWithoutFixAndNoAiTargets_ShowsNothing()
    {
        var viewModel = new ReviewFindingViewModel(Finding("Timeline anchor unclear", "The backstory is not anchored."), canFixWithAi: false);

        Assert.Equal(ReviewFindingAction.None, viewModel.Action);
    }
}
